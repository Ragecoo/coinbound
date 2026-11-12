using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coinbound
{
    /// <summary>Offline bilingual keyword recognition. Audio stays in bounded RAM queues, never on disk.</summary>
    public sealed class DungeonVoiceInput : MonoBehaviour
    {
        private const int Rate = 16000, Block = 480;
        private DungeonSpellcaster caster;
        private AudioClip microphone;
        private string device;
        private float[] input;
        private int readPosition, session;
        private float retryAt, lastAcceptedAt = -10;
        private DungeonSpell lastAccepted;
        private Worker worker;
        private DungeonVoiceDebug debug;
        private bool debugEnabled;
        public static string Status { get; private set; } = "";
        public void ToggleDebug()
        {
            debugEnabled=!debugEnabled;
            if(debug==null){debug=gameObject.AddComponent<DungeonVoiceDebug>();debug.Initialize(this);}
            debug.Show(debugEnabled);
            if(worker!=null)worker.DiagnosticsEnabled=debugEnabled;
        }
        internal bool TryDiagnostic(out VoiceDiagnostic result)
        {
            if(worker!=null)return worker.Diagnostics.TryDequeue(out result);
            result=default;return false;
        }
        public void Bind(DungeonSpellcaster value) { StopMicrophone(); caster = value; }
        private void Update()
        {
            if(Keyboard.current?.f1Key.wasPressedThisFrame==true)ToggleDebug();
            bool active = !Application.isBatchMode && DungeonSettings.VoiceInput && caster != null && caster.Owner.CanAct && Time.timeScale > 0 && Application.isFocused;
            if (!active) { StopMicrophone(); return; }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (worker == null)
            {
                try
                {
                    Native.Load(Application.dataPath);
                    worker = new Worker(Path.Combine(Application.streamingAssetsPath,"Voice"));
                    worker.DiagnosticsEnabled=debugEnabled;
                    Status = "Загрузка голосовых команд…";
                }
                catch (Exception e) { Status = "Голос недоступен: " + e.Message; enabled=false; Debug.LogWarning(Status); return; }
            }
            if (worker.Error != null) { Status="Голос недоступен: "+worker.Error; StopMicrophone(); return; }
            if (!worker.Ready) return;
            if (microphone == null)
            {
                if (Time.unscaledTime < retryAt) return;
                retryAt=Time.unscaledTime+3;
                if (Microphone.devices.Length==0) { Status="Микрофон не найден"; return; }
                device=Microphone.devices[0];
                try { microphone=Microphone.Start(device,true,2,Rate); }
                catch (Exception e) { Status="Нет доступа к микрофону: "+e.Message; return; }
                if (microphone==null) { Status="Не удалось включить микрофон"; return; }
                if (microphone.frequency!=Rate) { StopMicrophone(); Status="Микрофон не поддерживает 16000 Гц"; return; }
                input=new float[Block*microphone.channels]; readPosition=0; session++;
                worker.Clear(session); Status="Голос: pulse · trace · ward · decoy · recall";
            }
            if (!Microphone.IsRecording(device)) { StopMicrophone(); Status="Микрофон отключён"; return; }
            int position=Microphone.GetPosition(device);
            if(position<0)return;
            int available=(position-readPosition+microphone.samples)%microphone.samples;
            // Never let old audio build up into delayed casts.
            if (available>Rate/2) { readPosition=position; session++;worker.Clear(session);return; }
            while(available>=Block)
            {
                if(!microphone.GetData(input,readPosition))break;
                float[] mono=worker.Rent();
                for(int i=0;i<Block;i++)
                {
                    float sum=0;for(int channel=0;channel<microphone.channels;channel++)sum+=input[i*microphone.channels+channel];
                    // Vosk float input uses PCM16 amplitude units, unlike Unity normalized audio.
                    mono[i]=Mathf.Clamp(sum/microphone.channels,-1,1)*32767;
                }
                worker.Enqueue(mono,session);
                readPosition=(readPosition+Block)%microphone.samples; available-=Block;
            }
            while(worker.Results.TryDequeue(out Recognition result))
            {
                if(result.Session!=session || !caster.Owner.CanAct)continue;
                if(Time.unscaledTime-lastAcceptedAt<.65f)continue;
                lastAccepted=result.Spell;lastAcceptedAt=Time.unscaledTime;
                caster.Recognized(result.Spell);
            }
#else
            Status="Голосовые команды пока доступны в Windows-сборке";
#endif
        }
        private void StopMicrophone()
        {
            if(microphone==null)return;
            Microphone.End(device);Destroy(microphone);microphone=null;input=null;
            session++;worker?.Clear(session); Status="Микрофон выключен";
        }
        private void OnApplicationFocus(bool focused){if(!focused)StopMicrophone();}
        private void OnDisable()=>StopMicrophone();
        private void OnDestroy(){StopMicrophone();worker?.Dispose();worker=null;}

        public static bool TryWord(string text,out DungeonSpell spell)
        {
            switch(text.Trim().ToLowerInvariant())
            {
                case "pulse": case "пульс":spell=DungeonSpell.Pulse;return true;
                case "trace": case "трейс": case "трей с":spell=DungeonSpell.Trace;return true;
                case "ward": case "вард":spell=DungeonSpell.Ward;return true;
                case "decoy": case "декой": case "де кой":spell=DungeonSpell.Decoy;return true;
                case "recall": case "рекол": case "реколл": case "ре кол":spell=DungeonSpell.Recall;return true;
                default:spell=default;return false;
            }
        }
        private readonly struct Recognition
        {
            public readonly DungeonSpell Spell;public readonly int Session;
            public Recognition(DungeonSpell spell,int session){Spell=spell;Session=session;}
        }
        private readonly struct AudioBlock
        {
            public readonly float[] Samples;public readonly int Session;
            public AudioBlock(float[] samples,int session){Samples=samples;Session=session;}
        }
        private sealed class Worker : IDisposable
        {
            public volatile bool Ready;
            public volatile string Error;
            public volatile bool DiagnosticsEnabled;
            public readonly ConcurrentQueue<VoiceDiagnostic> Diagnostics=new ConcurrentQueue<VoiceDiagnostic>();
            public readonly ConcurrentQueue<Recognition> Results=new ConcurrentQueue<Recognition>();
            private readonly ConcurrentQueue<AudioBlock> audio=new ConcurrentQueue<AudioBlock>();
            private readonly ConcurrentQueue<float[]> pool=new ConcurrentQueue<float[]>();
            private readonly AutoResetEvent signal=new AutoResetEvent(false);
            private readonly Thread thread;
            private volatile bool stop;
            private int desiredSession;
            private int resetVersion;
            private static readonly Regex Text=new Regex("\\\"text\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"",RegexOptions.Compiled);
            private static readonly Regex Confidence=new Regex("\\\"conf\\\"\\s*:\\s*([0-9.]+)",RegexOptions.Compiled);
            public Worker(string root){thread=new Thread(()=>Run(root)){IsBackground=true,Name="Coinbound offline voice"};thread.Start();}
            public float[] Rent()=>pool.TryDequeue(out float[] value)?value:new float[Block];
            public void Enqueue(float[] samples,int session)
            {
                if(audio.Count>=12){pool.Enqueue(samples);Clear(session);return;}
                audio.Enqueue(new AudioBlock(samples,session));signal.Set();
            }
            public void Clear(int session)
            {
                Volatile.Write(ref desiredSession,session);
                Interlocked.Increment(ref resetVersion);
                while(audio.TryDequeue(out AudioBlock block))pool.Enqueue(block.Samples);
                while(Results.TryDequeue(out _)){} signal.Set();
                while(Diagnostics.TryDequeue(out _)){}
            }
            private void Run(string root)
            {
                IntPtr en=IntPtr.Zero,ru=IntPtr.Zero,enRec=IntPtr.Zero,ruRec=IntPtr.Zero,debugEn=IntPtr.Zero,debugRu=IntPtr.Zero;
                try
                {
                    Native.vosk_set_log_level(-1);
                    en=Native.vosk_model_new(Path.Combine(root,"en"));ru=Native.vosk_model_new(Path.Combine(root,"ru"));
                    if(en==IntPtr.Zero || ru==IntPtr.Zero)throw new InvalidOperationException("Не найдены языковые модели");
                    enRec=Native.vosk_recognizer_new_grm(en,Rate,"[\"pulse\",\"trace\",\"ward\",\"decoy\",\"recall\",\"[unk]\"]");
                    // Russian model lacks the loanwords as single tokens; these dictionary-backed
                    // phonetic phrases decode the same spoken sounds without editing the model.
                    ruRec=Native.vosk_recognizer_new_grm(ru,Rate,"[\"пульс\",\"трей с\",\"вард\",\"де кой\",\"ре кол\",\"[unk]\"]");
                    if(enRec==IntPtr.Zero || ruRec==IntPtr.Zero)throw new InvalidOperationException("Не удалось создать распознавание");
                    Native.vosk_recognizer_set_words(enRec,1);Native.vosk_recognizer_set_words(ruRec,1);
                    int currentSession=-1,currentReset=-1;string enPartial="",ruPartial="";Ready=true;
                    while(!stop)
                    {
                        int wanted=Volatile.Read(ref desiredSession);
                        int reset=Volatile.Read(ref resetVersion);
                        if(DiagnosticsEnabled && debugEn==IntPtr.Zero)
                        {
                            debugEn=Native.vosk_recognizer_new(en,Rate);debugRu=Native.vosk_recognizer_new(ru,Rate);
                            if(debugEn==IntPtr.Zero || debugRu==IntPtr.Zero)throw new InvalidOperationException("Не удалось включить голосовую отладку");
                            enPartial=ruPartial="";
                        }
                        else if(!DiagnosticsEnabled && debugEn!=IntPtr.Zero)
                        {Native.vosk_recognizer_free(debugEn);Native.vosk_recognizer_free(debugRu);debugEn=debugRu=IntPtr.Zero;}
                        if(currentSession!=wanted || currentReset!=reset)
                        {
                            Native.vosk_recognizer_reset(enRec);Native.vosk_recognizer_reset(ruRec);
                            if(debugEn!=IntPtr.Zero){Native.vosk_recognizer_reset(debugEn);Native.vosk_recognizer_reset(debugRu);enPartial=ruPartial="";}
                            currentSession=wanted;currentReset=reset;
                        }
                        if(!audio.TryDequeue(out AudioBlock block)){signal.WaitOne(40);continue;}
                        try
                        {
                            if(block.Session!=currentSession)continue;
                            Decode(enRec,block,currentReset);Decode(ruRec,block,currentReset);
                            if(debugEn!=IntPtr.Zero)
                            {
                                Diagnose(debugEn,block,"EN",ref enPartial,currentReset);
                                Diagnose(debugRu,block,"RU",ref ruPartial,currentReset);
                            }
                        }
                        finally{pool.Enqueue(block.Samples);}
                    }
                }
                catch(Exception e){Error=e.Message;}
                finally
                {
                    Ready=false;
                    if(enRec!=IntPtr.Zero)Native.vosk_recognizer_free(enRec);if(ruRec!=IntPtr.Zero)Native.vosk_recognizer_free(ruRec);
                    if(debugEn!=IntPtr.Zero)Native.vosk_recognizer_free(debugEn);if(debugRu!=IntPtr.Zero)Native.vosk_recognizer_free(debugRu);
                    if(en!=IntPtr.Zero)Native.vosk_model_free(en);if(ru!=IntPtr.Zero)Native.vosk_model_free(ru);

                }
            }
            private void Decode(IntPtr recognizer,AudioBlock block,int reset)
            {
                int accepted=Native.vosk_recognizer_accept_waveform_f(recognizer,block.Samples,block.Samples.Length);
                if(accepted<0)throw new InvalidOperationException("Ошибка обработки аудио");
                if(accepted==0)return;
                string json=Native.Utf8(Native.vosk_recognizer_result(recognizer));
                Match text=Text.Match(json);if(!text.Success || !TryWord(text.Groups[1].Value,out DungeonSpell spell))return;
                MatchCollection words=Confidence.Matches(json);if(words.Count==0)return;
                foreach(Match word in words)if(double.Parse(word.Groups[1].Value,CultureInfo.InvariantCulture)<.90)return;
                if(block.Session==Volatile.Read(ref desiredSession) && reset==Volatile.Read(ref resetVersion))
                    Results.Enqueue(new Recognition(spell,block.Session));
            }
            private static readonly Regex Partial=new Regex("\\\"partial\\\"\\s*:\\s*\\\"([^\\\"]*)\\\"",RegexOptions.Compiled);
            private void Diagnose(IntPtr recognizer,AudioBlock block,string language,ref string previous,int reset)
            {
                int accepted=Native.vosk_recognizer_accept_waveform_f(recognizer,block.Samples,block.Samples.Length);
                if(accepted<0)return;
                bool final=accepted==1;
                string json=Native.Utf8(final?Native.vosk_recognizer_result(recognizer):Native.vosk_recognizer_partial_result(recognizer));
                Match match=(final?Text:Partial).Match(json);if(!match.Success)return;
                string words=match.Groups[1].Value;
                if(!final && words==previous)return;
                previous=final?"":words;
                if(reset!=Volatile.Read(ref resetVersion) || !DiagnosticsEnabled)return;
                while(Diagnostics.Count>=100)Diagnostics.TryDequeue(out _);
                Diagnostics.Enqueue(new VoiceDiagnostic(language,words,final));
            }
            public void Dispose(){stop=true;signal.Set();if(thread.Join(500))signal.Dispose();}
        }
        private static class Native
        {
            private const string Lib="libvosk";
            [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
            public static void Load(string dataPath)
            {
                string file=Path.Combine(dataPath,"Plugins","x86_64","libvosk.dll");
                if(LoadLibraryEx(file,IntPtr.Zero,0x1100)==IntPtr.Zero)throw new InvalidOperationException("Vosk DLL: "+Marshal.GetLastWin32Error());
            }
            public static string Utf8(IntPtr pointer){int length=0;while(Marshal.ReadByte(pointer,length)!=0)length++;byte[] bytes=new byte[length];Marshal.Copy(pointer,bytes,0,length);return Encoding.UTF8.GetString(bytes);}
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void vosk_set_log_level(int level);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern IntPtr vosk_model_new([MarshalAs(UnmanagedType.LPUTF8Str)]string path);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void vosk_model_free(IntPtr model);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern IntPtr vosk_recognizer_new_grm(IntPtr model,float rate,[MarshalAs(UnmanagedType.LPUTF8Str)]string grammar);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern IntPtr vosk_recognizer_new(IntPtr model,float rate);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void vosk_recognizer_set_words(IntPtr recognizer,int words);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern int vosk_recognizer_accept_waveform_f(IntPtr recognizer,float[] samples,int length);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern IntPtr vosk_recognizer_result(IntPtr recognizer);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern IntPtr vosk_recognizer_partial_result(IntPtr recognizer);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void vosk_recognizer_reset(IntPtr recognizer);
            [DllImport(Lib,CallingConvention=CallingConvention.Cdecl)]public static extern void vosk_recognizer_free(IntPtr recognizer);
        }
    }
    internal readonly struct VoiceDiagnostic
    {
        public readonly string Language,Text;public readonly bool Final;
        public VoiceDiagnostic(string language,string text,bool final){Language=language;Text=text;Final=final;}
    }
}
