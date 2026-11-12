using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ProceduralGeneration;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Coinbound.Tests
{
    public sealed class DungeonEnvironmentTests
    {
        [Test] public void ThemedDecorPreservesAuthoredSpriteSizesAndPivots()
        {
            var catalog=DungeonPropCatalog.ForFloor(2);Assert.That(catalog.assets.Length,Is.EqualTo(80));
            foreach(var data in catalog.assets)
            {
                Assert.That(data.Sprite,Is.Not.Null,data.name);
                Assert.That(data.Sprite.rect.size,Is.EqualTo(new Vector2(data.width,data.height)),data.name);
                Assert.That(data.Sprite.pixelsPerUnit,Is.EqualTo(32),data.name);
                Assert.That(data.Sprite.pivot.x,Is.EqualTo(data.pivot[0]).Within(.001f),data.name);
                Assert.That(data.Sprite.pivot.y,Is.EqualTo(data.pivot[1]).Within(.001f),data.name);
                Assert.That(data.Sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                if(data.themes.Contains("plants")){Assert.That(data.solid,Is.False);Assert.That(data.hp,Is.Zero);}
                foreach(var component in data.components)Assert.That(catalog.Get(component.id).Sprite,Is.Not.Null);
            }
            foreach(int floor in new[]{2,3})
            {
                Assert.That(Resources.Load<Sprite>("DungeonArt_Floor"+floor+"/wall.01"),Is.Not.Null);
                Assert.That(Resources.Load<Sprite>("DungeonArt_Floor"+floor+"/object.fountain").pixelsPerUnit,Is.EqualTo(40));
            }
        }
        [Test] public void FountainFramesPreserveLandmarkGeometry()
        {
            for(int i=0;i<24;i++)
            {
                var sprite=Resources.Load<Sprite>("FountainAnimation/fountain_"+i.ToString("00"));
                Assert.That(sprite,Is.Not.Null);
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(120,120)));
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(60,60)));
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(40));
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
            }
        }
        [Test] public void EveryAuthoredSpriteKeepsItsSizePivotAndPhysicsData()
        {
            var catalog=DungeonPropCatalog.Current;Assert.That(catalog.assets.Length,Is.EqualTo(52));
            Assert.That(catalog.assets.Count(a=>a.hp>0),Is.EqualTo(25));
            foreach(var data in catalog.assets)
            {
                var sprite=data.Sprite;Assert.That(sprite,Is.Not.Null,data.name);
                Assert.That(sprite.rect.size,Is.EqualTo(new Vector2(data.width,data.height)),data.name);
                Assert.That(sprite.pivot,Is.EqualTo(new Vector2(data.pivot[0],data.pivot[1])),data.name);
                Assert.That(sprite.pixelsPerUnit,Is.EqualTo(32));Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));
                if(!data.solid)Assert.That(data.boxes,Is.Empty,data.name);
                if(data.tall)Assert.That(data.walls,Is.EqualTo(new[]{"N"}));
            }
            foreach(string name in new[]{"debris_crate","debris_barrel","debris_longwood","debris_pottery"})
                Assert.That(Resources.Load<Sprite>("EnvironmentProps52V5/debris/"+name).pivot,Is.EqualTo(new Vector2(16,8)));
        }
        [Test] public void AllAllowedWallAndCornerContactsHaveZeroPixelGap()
        {
            Rect bounds=new Rect(0,0,20,20);
            foreach(var data in DungeonPropCatalog.Current.assets)
                foreach(string wall in data.corner.Length>0?new[]{data.corner}:data.walls)
                {
                    Vector2 root=DungeonProps.FlushRoot(data,bounds.center,bounds,wall);
                    if(wall.Contains("N"))Assert.That(root.y+(data.contacts[0]-data.pivot[1])/32f,Is.EqualTo(bounds.yMax));
                    if(wall.Contains("E"))Assert.That(root.x+(data.contacts[1]-data.pivot[0])/32f,Is.EqualTo(bounds.xMax));
                    if(wall.Contains("S"))Assert.That(root.y+(data.contacts[2]-data.pivot[1])/32f,Is.EqualTo(bounds.yMin));
                    if(wall.Contains("W"))Assert.That(root.x+(data.contacts[3]-data.pivot[0])/32f,Is.EqualTo(bounds.xMin));
                }
        }
        [Test] public void LightIntersectionsFollowGeometryAndLeaveSourceBaseUnshadowed()
        {
            var boxes=new[]{new Rect(2,-.5f,1,1)};
            Assert.That(DungeonProps.TraceShadow(boxes,Vector2.zero,Vector2.right,10),Is.EqualTo(new Vector2(2,3)));
            Assert.That(DungeonProps.TraceShadow(boxes,Vector2.zero,Vector2.up,10),Is.EqualTo(new Vector2(10,10)));
            Assert.That(DungeonProps.TraceShadow(boxes,new Vector2(2.5f,0),Vector2.right,10),Is.EqualTo(new Vector2(10,10)));
            Assert.That(DungeonProps.TraceShadow(new Rect[0],Vector2.zero,Vector2.right,10),Is.EqualTo(new Vector2(10,10)));
        }
        [Test] public void HiddenSourcesCannotLightVisibleFloor()
        {
            var generator=new DungeonGenerator(new GenerationSettings(Seed:42));foreach(var step in generator.Generate()){}
            var map=generator.Map;Vector2 player=DungeonView.WorldPosition(map.EntranceTile.Value);
            var obj=new GameObject("Visibility camera",typeof(Camera));var camera=obj.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=30;obj.transform.position=new Vector3(player.x,player.y,-10);
            try
            {
                Assert.That(DungeonEffectLights.AmbientVisible(map,player,player+Vector2.right,camera),Is.True);
                bool checkedHidden=false;
                for(int y=0;y<map.TileHeight;y++)for(int x=0;x<map.TileWidth;x++)
                {
                    var tile=new TilePosition(x,y);if(!map.IsTileFloor(tile))continue;Vector2 point=DungeonView.WorldPosition(tile),delta=point-player;
                    if(delta.magnitude<13&&DungeonVisibility.Cast(map,player,delta,delta.magnitude)<delta.magnitude-.1f)
                    {Assert.That(DungeonEffectLights.AmbientVisible(map,player,point,camera),Is.False);checkedHidden=true;}
                }
                Assert.That(checkedHidden,Is.True);
                obj.transform.position+=Vector3.right*100;
                Assert.That(DungeonEffectLights.AmbientVisible(map,player,player+Vector2.right,camera),Is.False);
            }
            finally{Object.DestroyImmediate(obj);}
        }
        [Test] public void AmbientActivationIncludesThreeTilesBeyondCamera()
        {
            var obj=new GameObject("Activation camera",typeof(Camera));
            try
            {
                var camera=obj.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.aspect=1;obj.transform.position=new Vector3(0,0,-10);
                Assert.That(DungeonEffectLights.AmbientActive(new Vector2(7.9f,0),camera),Is.True);
                Assert.That(DungeonEffectLights.AmbientActive(new Vector2(8.1f,0),camera),Is.False);
                Assert.That(DungeonEffectLights.AmbientActive(new Vector2(0,7.9f),camera),Is.True);
            }
            finally{Object.DestroyImmediate(obj);}
        }
        [UnityTest] public IEnumerator SeededPropsPreserveRoutesDamageAndSurvivors()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var game=Object.FindAnyObjectByType<DungeonGame>();JsonUtility.FromJsonOverwrite("{\"randomSeed\":false,\"seed\":42,\"minimumStatusSeconds\":0}",game);game.StartNewDungeon();
            float timeout=Time.realtimeSinceStartup+50;while(!game.IsReady&&Time.realtimeSinceStartup<timeout)yield return null;
            Assert.That(game.IsReady,Is.True);var props=DungeonProps.Current;Assert.That(props.Items.Count,Is.InRange(10,512));Assert.That(props.TorchCount,Is.GreaterThan(1));
            for(int i=0;i<game.Map.Rooms.Count;i++)Assert.That(props.ConnectedRoom(i),Is.True,"Room "+i);
            var torches=(System.Collections.Generic.List<Vector2>)typeof(DungeonProps).GetField("torches",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(props);
            foreach(var torch in torches)
            {
                System.Func<Vector2,bool> floor=p=>game.Map.IsTileFloor(new TilePosition(Mathf.FloorToInt(p.x),Mathf.FloorToInt(1-p.y)));
                Vector2 normal=!floor(torch+Vector2.left*.25f)?Vector2.left:!floor(torch+Vector2.right*.25f)?Vector2.right:Vector2.up;
                Assert.That(props.StraightTorchWall(torch,normal),Is.True,"Torch must stay away from corners");
            }
            var fountain=Object.FindAnyObjectByType<DungeonFountain>();
            var visual=fountain.GetComponent<SpriteRenderer>();var firstFrame=visual.sprite;
            yield return new WaitForSeconds(.3f);
            Assert.That(visual.sprite,Is.Not.SameAs(firstFrame),"Fountain must advance automatically");
            Assert.That(visual.sprite.name,Does.StartWith("fountain_"));
            var corridorProps=props.Items.Where(p=>p.Room<0).ToArray();
            Assert.That(corridorProps.Length,Is.GreaterThan(0));
            foreach(var prop in corridorProps)
            {
                Assert.That(prop.Definition.solid,Is.False);
                Assert.That(prop.GetComponentsInChildren<Collider2D>(),Is.Empty);
                foreach(var other in corridorProps)if(other!=prop)Assert.That(Vector2.Distance(prop.transform.position,other.transform.position),Is.GreaterThan(6));
            }
            var snapshots=props.Snapshot();
            foreach(var item in props.Items){Assert.That(item.transform.localScale,Is.EqualTo(Vector3.one));Assert.That(item.transform.rotation,Is.EqualTo(Quaternion.identity));}
            var spawn=typeof(DungeonProps).GetMethod("Spawn",BindingFlags.Instance|BindingFlags.NonPublic);
            Vector2 location=DungeonProps.RoomBounds(game.Map.Rooms[0]).center;
            var crate=(DungeonProp)spawn.Invoke(props,new object[]{DungeonPropCatalog.Current.Get(28),999999,0,"N",location});props.Items.Add(crate);
            Assert.That(crate.ReceiveHit(float.NaN,DungeonProps.NewAttackId()),Is.False);
            Time.timeScale=0;Assert.That(crate.ReceiveHit(999,DungeonProps.NewAttackId()),Is.False);Time.timeScale=1;
            int shadowVersion=props.ShadowRevision;
            Assert.That(crate.ReceiveHit(.001f,DungeonProps.NewAttackId()),Is.True);Assert.That(crate.Destroyed,Is.True);
            yield return null;Assert.That(props.ShadowRevision,Is.GreaterThan(shadowVersion));
            Assert.That(crate.GetComponentsInChildren<Collider2D>().Count(c=>c.enabled),Is.EqualTo(DungeonPropCatalog.Current.Get(13).boxes.Length));
            Assert.That(crate.ReceiveHit(999,DungeonProps.NewAttackId()),Is.False);
            var saved=props.Snapshot();props.Restore(saved);yield return null;
            Assert.That(props.Items.Count(p=>p.Room<0),Is.EqualTo(corridorProps.Length));
            var loaded=props.Items.Single(p=>p.Id==999999);Assert.That(loaded.Destroyed,Is.True);
            Assert.That(loaded.GetComponentsInChildren<Collider2D>().Count(c=>c.enabled),Is.EqualTo(DungeonPropCatalog.Current.Get(13).boxes.Length));
            Debug.Log("ENVIRONMENT_CHECK props="+snapshots.Length+" torches="+props.TorchCount+" particles="+props.ActiveParticleCount);
            game.StartNewDungeon();timeout=Time.realtimeSinceStartup+50;while(!game.IsReady&&Time.realtimeSinceStartup<timeout)yield return null;
            Assert.That(game.IsReady,Is.True);var again=DungeonProps.Current.Snapshot();Assert.That(again.Length,Is.EqualTo(snapshots.Length));
            for(int i=0;i<again.Length;i++){Assert.That(again[i].asset,Is.EqualTo(snapshots[i].asset));Assert.That(again[i].position,Is.EqualTo(snapshots[i].position));}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var game=Object.FindAnyObjectByType<DungeonGame>();if(game!=null)Object.Destroy(game.gameObject);if(Camera.main!=null)Object.Destroy(Camera.main.gameObject);Time.timeScale=1;yield return null;
        }
    }
}
