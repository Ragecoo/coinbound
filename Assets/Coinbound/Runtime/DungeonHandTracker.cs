using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
namespace Coinbound
{
    internal sealed class DungeonHandTracker : IDisposable
    {
        [Serializable] internal sealed class Result {public Vector3[] points,world;}
        private readonly BlockingCollection<byte[]> frames=new BlockingCollection<byte[]>(1);
        internal readonly ConcurrentQueue<string> Results=new ConcurrentQueue<string>();
        private Process process;
        private readonly ProcessStartInfo processInfo;
        private Thread thread;
        private volatile bool stopping;
        public volatile bool Ready;
        public string Error {get;private set;}
        public DungeonHandTracker(string directory)
        {
            processInfo=new ProcessStartInfo {
                FileName=Path.Combine(directory,"python","python.exe"),
                Arguments="-B -u \""+Path.Combine(directory,"track.py")+"\"",
                WorkingDirectory=directory,UseShellExecute=false,CreateNoWindow=true,
                RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true
            };
            thread=new Thread(Run){IsBackground=true,Name="Coinbound hand landmarks"};thread.Start();
        }
        public void Submit(byte[] rgb)
        {if(!Ready||stopping)return;try{frames.TryAdd(rgb);}catch(InvalidOperationException){}}
        private void Run()
        {
            try
            {
                process=Process.Start(processInfo);
                if(stopping){try{process.Kill();process.Dispose();}catch(Exception){}return;}
                process.ErrorDataReceived+=(s,e)=>{};
                process.BeginErrorReadLine();
                string hello=process.StandardOutput.ReadLine();
                if(hello==null||!hello.Contains("ready"))throw new IOException("Hand tracker failed to initialize.");
                Ready=true;
                using var writer=new BinaryWriter(process.StandardInput.BaseStream);
                foreach(byte[] frame in frames.GetConsumingEnumerable())
                {
                    if(stopping)break;
                    writer.Write(384);writer.Write(288);writer.Write(frame);writer.Flush();
                    string result=process.StandardOutput.ReadLine();
                    if(result==null)throw new IOException("Hand tracker stopped.");
                    while(Results.Count>1)Results.TryDequeue(out _);
                    Results.Enqueue(result);
                }
            }
            catch(Exception e){if(!stopping)Error=e.Message;}
            finally{Ready=false;}
        }
        public void Dispose()
        {
            if(stopping)return;stopping=true;frames.CompleteAdding();
            try{if(process!=null){process.StandardInput.Close();if(!process.HasExited)process.Kill();}}catch(Exception){}
            thread?.Join(250);process?.Dispose();
        }
    }
}
