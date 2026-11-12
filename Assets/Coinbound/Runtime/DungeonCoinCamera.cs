using System;
using System.Collections;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Local hand-landmark input. Camera frames travel only through an anonymous process pipe.</summary>
    public sealed class DungeonCoinCamera : MonoBehaviour
    {
        public event Action Tossed;
        public WebCamTexture Feed {get;private set;}
        public Texture2D Preview {get;private set;}
        public string Status {get;private set;}="Камера выключена";
        public int Stage=>gesture.Stage;
        public Vector3[] HandPoints {get;private set;}
        private readonly DungeonCoinGesture gesture=new DungeonCoinGesture();
        private DungeonHandTracker tracker;
        private Color32[] pixels;
        private readonly Color32[] previewPixels=new Color32[160*120];
        private Coroutine startup;
        private float openedAt,nextSample,lastSample,trackerStarted;
        private int deviceIndex;
        public void StartCamera(bool next=false)
        {
            StopCamera();
            if(next)deviceIndex++;
            startup=StartCoroutine(Open());
        }
        private IEnumerator Open()
        {
            Status="Запрашиваю доступ к камере…";
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if(!Application.HasUserAuthorization(UserAuthorization.WebCam)){Status="Нет доступа. Разрешите камеру в Windows.";startup=null;yield break;}
            var devices=WebCamTexture.devices;
            if(devices.Length==0){Status="Камера не найдена. Доступен обычный подброс.";startup=null;yield break;}
            try
            {
                tracker=new DungeonHandTracker(System.IO.Path.Combine(Application.streamingAssetsPath,"Hands"));
                trackerStarted=Time.unscaledTime;
                Feed=new WebCamTexture(devices[deviceIndex%devices.Length].name,640,480,30);
                Feed.filterMode=FilterMode.Point;
                Feed.Play();openedAt=lastSample=Time.unscaledTime;
                Status="Приготовьтесь: ладонь перед камерой";
            }
            catch(Exception){StopCamera();Status="Камера занята или недоступна.";}
            startup=null;
        }
        private void Update()
        {
            if(Feed==null)return;
            if(tracker.Error!=null){StopCamera();Status="Распознавание недоступно. Используйте обычный подброс.";return;}
            if(!tracker.Ready)
            {
                Status="Загрузка распознавания кисти…";
                if(Time.unscaledTime-trackerStarted>20){StopCamera();Status="Не удалось запустить распознавание.";}
                return;
            }
            string latest=null;
            while(tracker.Results.TryDequeue(out string message))latest=message;
            if(latest!=null)
            {
                var result=JsonUtility.FromJson<DungeonHandTracker.Result>(latest);
                float dt=Time.unscaledTime-lastSample;lastSample=Time.unscaledTime;
                bool detected=result.points!=null&&result.points.Length==21;
                DrawSkeleton(result.points);
                if(gesture.Observe(result.points,result.world,dt)){Tossed?.Invoke();return;}
                Status=!detected?"Покажите кулак":gesture.Stage switch {
                    0=>"Сожмите кулак",
                    1=>"Кулак: "+Mathf.RoundToInt(gesture.HoldProgress*100)+"%",
                    _=>"Покажите лайк"
                };
            }
            if(Feed.width<=16||!Feed.didUpdateThisFrame)
            {
                if(Time.unscaledTime-openedAt>8){StopCamera();Status="Нет кадров. Проверьте камеру или выберите другую.";}
                return;
            }
            openedAt=Time.unscaledTime;
            if(Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.05f;
            if(pixels==null||pixels.Length!=Feed.width*Feed.height)pixels=new Color32[Feed.width*Feed.height];
            try{Feed.GetPixels32(pixels);}catch(Exception){StopCamera();Status="Не удалось прочитать кадр камеры.";return;}
            var rgb=new byte[384*288*3];
            for(int y=0;y<288;y++)for(int x=0;x<384;x++)
            {
                float u=(x+.5f)/384,v=1-(y+.5f)/288;
                float rx=u,ry=v;
                switch(Feed.videoRotationAngle){case 90:rx=1-v;ry=u;break;case 180:rx=1-u;ry=1-v;break;case 270:rx=v;ry=1-u;break;}
                if(Feed.videoVerticallyMirrored)ry=1-ry;
                Color32 c=pixels[Mathf.Clamp((int)(ry*Feed.height),0,Feed.height-1)*Feed.width+Mathf.Clamp((int)(rx*Feed.width),0,Feed.width-1)];
                int index=(y*384+x)*3;rgb[index]=c.r;rgb[index+1]=c.g;rgb[index+2]=c.b;
            }
            tracker.Submit(rgb);
        }
        private void DrawSkeleton(Vector3[] points)
        {
            HandPoints=points!=null&&points.Length==21?points:null;
            if(Preview==null){Preview=new Texture2D(160,120,TextureFormat.RGBA32,false);Preview.filterMode=FilterMode.Point;Preview.wrapMode=TextureWrapMode.Clamp;}
            Array.Clear(previewPixels,0,previewPixels.Length);
            if(points!=null&&points.Length==21)
            {
                int[] edges={0,1,1,2,2,3,3,4,0,5,5,6,6,7,7,8,5,9,9,10,10,11,11,12,9,13,13,14,14,15,15,16,13,17,0,17,17,18,18,19,19,20};
                Vector2Int Pixel(int i)=>new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((1-points[i].x)*159),0,159),Mathf.Clamp(Mathf.RoundToInt((1-points[i].y)*119),0,119));
                Color32 bone=new Color32(0,166,236,255),joint=new Color32(72,198,255,255);
                for(int e=0;e<edges.Length;e+=2)
                {
                    var a=Pixel(edges[e]);var b=Pixel(edges[e+1]);int steps=Mathf.Max(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y));
                    for(int k=0;k<=steps;k++){float t=steps==0?0:k/(float)steps;int x=Mathf.RoundToInt(Mathf.Lerp(a.x,b.x,t)),y=Mathf.RoundToInt(Mathf.Lerp(a.y,b.y,t));previewPixels[y*160+x]=bone;}
                }
                for(int i=0;i<21;i++){var a=Pixel(i);previewPixels[a.y*160+a.x]=joint;if(a.x<159)previewPixels[a.y*160+a.x+1]=joint;}
            }
            Preview.SetPixels32(previewPixels);Preview.Apply(false);
        }
        public void StopCamera()
        {
            if(startup!=null){StopCoroutine(startup);startup=null;}
            if(Feed!=null){Feed.Stop();Destroy(Feed);Feed=null;}
            if(Preview!=null){Destroy(Preview);Preview=null;}
            tracker?.Dispose();tracker=null;
            pixels=null;HandPoints=null;gesture.Reset();Status="Камера выключена";
        }
        private void OnDisable()=>StopCamera();
        private void OnDestroy()=>StopCamera();
    }
}
