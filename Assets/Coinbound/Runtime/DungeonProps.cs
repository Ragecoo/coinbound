using System;
using System.Collections.Generic;
using ProceduralGeneration;
using UnityEngine;
namespace Coinbound
{
    public sealed partial class DungeonProps : MonoBehaviour
    {
        public static DungeonProps Current {get;private set;}
        public readonly List<DungeonProp> Items=new List<DungeonProp>();
        private DungeonPropCatalog catalog;private int floorNumber;
        internal PropDefinition Definition(int id)=>catalog.Get(id);
        private DungeonMap map;private SpawnPlan spawns;private DungeonPlayer player;private Material material;
        private Camera cameraView;private PropRecipes recipes;private System.Random random;private bool dirty;
        private static int attackSequence;
        public bool CanAct=>player!=null&&player.CanAct&&Time.timeScale>0;
        public static int NewAttackId()=>++attackSequence;
        public static DungeonProps Create(DungeonMap map,SpawnPlan spawns,DungeonPlayer player,int seed,int floorNumber=1)
        {
            var obj=new GameObject("Environment props V5");obj.transform.SetParent(player.transform.parent,false);
            var props=obj.AddComponent<DungeonProps>();props.map=map;props.spawns=spawns;props.player=player;props.material=player.EffectMaterial;
            props.floorNumber=floorNumber;props.catalog=DungeonPropCatalog.ForFloor(floorNumber);props.cameraView=Camera.main;props.random=new System.Random(unchecked(seed^0x416C57));Current=props;
            props.recipes=JsonUtility.FromJson<PropRecipes>(Resources.Load<TextAsset>("EnvironmentProps52V5/recipes").text);
            props.Build();props.BuildEffects();Physics2D.SyncTransforms();props.RebuildShadowGeometry();DungeonCombatNavigation.Invalidate(map);return props;
        }
        public static Rect RoomBounds(DungeonRoom room)=>new Rect(room.TileLeft,1-room.TileTop-room.TileHeight,room.TileWidth,room.TileHeight);
        public static Vector2 FlushRoot(PropDefinition data,Vector2 initial,Rect bounds,string wall)
        {
            if(wall.Contains("N"))initial.y=bounds.yMax-(data.contacts[0]-data.pivot[1])/32f;
            if(wall.Contains("S"))initial.y=bounds.yMin-(data.contacts[2]-data.pivot[1])/32f;
            if(wall.Contains("W"))initial.x=bounds.xMin-(data.contacts[3]-data.pivot[0])/32f;
            if(wall.Contains("E"))initial.x=bounds.xMax-(data.contacts[1]-data.pivot[0])/32f;
            return new Vector2(Mathf.Round(initial.x*32)/32,Mathf.Round(initial.y*32)/32);
        }
        private void Build()
        {
            if(floorNumber>1){BuildThemed();BuildCorridorDressing();PlanPassageAndSideTorches();return;}
            string[] themes={"storage","crypt","damp","camp","sanctuary"};
            for(int roomId=0;roomId<map.Rooms.Count;roomId++)
            {
                var room=map.Rooms[roomId];Rect bounds=RoomBounds(room);string theme=themes[random.Next(themes.Length)];int index=0;
                // Pack authored wall recipes, never rotate their contents or fall back to the room centre.
                int groups=Mathf.Clamp((room.TileWidth+room.TileHeight)/9,2,4);
                for(int group=0;group<groups;group++)
                {
                    var choices=new List<PropRecipe>();foreach(var recipe in recipes.recipes)if(recipe.theme==theme)choices.Add(recipe);
                    if(choices.Count==0)foreach(var recipe in recipes.recipes)choices.Add(recipe);
                    for(int attempt=0;attempt<6;attempt++)
                    {
                        var recipe=choices[random.Next(choices.Count)];if(recipe.allowed_walls==null||recipe.allowed_walls.Length==0)continue;
                        string wall=recipe.allowed_walls[random.Next(recipe.allowed_walls.Length)];if(wall.Length!=1)continue;
                        bool horizontal=wall=="N"||wall=="S";
                        int start=random.Next(1,Mathf.Max(2,(int)(horizontal?bounds.width:bounds.height)-2));int added=0;
                        foreach(var member in recipe.members)
                        {
                            var definition=catalog.Get(member.asset_id);if(definition==null)continue;
                            Vector2 initial=horizontal?new Vector2(bounds.xMin+start+member.tangent_cell+.5f,bounds.center.y):new Vector2(bounds.center.x,bounds.yMax-start-member.tangent_cell-.5f);
                            if(TryPlace(definition,roomId,wall,initial,roomId*1000+index)){index++;added++;}
                        }
                        if(added>0)break;
                    }
                }
                // An occasional compatible standalone asset keeps all 52 variants available to the seeded planner.
                for(int i=0;i<6;i++)
                {
                    var data=catalog.Get(1+random.Next(48));if(Array.IndexOf(data.themes,theme)<0||data.walls.Length==0)continue;
                    string wall=data.walls[random.Next(data.walls.Length)];
                    var initial=new Vector2(bounds.xMin+random.Next(1,Mathf.Max(2,(int)bounds.width-1))+.5f,bounds.yMin+random.Next(1,Mathf.Max(2,(int)bounds.height-1))+.5f);
                    if(TryPlace(data,roomId,wall,initial,roomId*1000+index)){index++;break;}
                }
                if(room.TileWidth>=8&&room.TileHeight>=8)
                    for(int i=0;i<2;i++)
                    {
                        var corner=catalog.Get(49+random.Next(4));
                        if(TryPlace(corner,roomId,corner.corner,bounds.center,roomId*1000+index))index++;
                    }
                PlanTorches(roomId,bounds);
            }
            BuildCorridorDressing();
            PlanPassageAndSideTorches();
        }
        private void BuildThemed()
        {
            var choices=new List<PropDefinition>();foreach(var data in catalog.assets)if(data.placeable&&data.floor==floorNumber)choices.Add(data);
            for(int roomId=0;roomId<map.Rooms.Count;roomId++)
            {
                Rect bounds=RoomBounds(map.Rooms[roomId]);int placed=0;
                int desired=floorNumber==3?10:6;
                for(int attempt=0;attempt<80&&placed<desired;attempt++)
                {
                    var data=choices[random.Next(choices.Count)];if(data.walls.Length==0)continue;
                    string wall=data.walls[random.Next(data.walls.Length)];
                    Vector2 initial=new Vector2(bounds.xMin+1+random.Next(Mathf.Max(1,(int)bounds.width-2))+.5f,bounds.yMin+1+random.Next(Mathf.Max(1,(int)bounds.height-2))+.5f);
                    if(TryPlace(data,roomId,wall,initial,roomId*1000+placed))placed++;
                }
                PlanTorches(roomId,bounds);
            }
        }
        private void BuildCorridorDressing()
        {
            // Small flat props on corridor edges leave the entire walking lane unobstructed.
            var candidates=new List<PropDefinition>();
            foreach(var data in catalog.assets)
                if((floorNumber==1||data.floor==floorNumber&&data.placeable)&&!data.solid&&data.ground&&data.width==32&&data.height==32&&data.corner.Length==0)candidates.Add(data);
            var corridorRandom=new System.Random(random.Next());
            int sequence=0;
            for(int y=2;y<map.TileHeight-2;y++)for(int x=2;x<map.TileWidth-2;x++)
            {
                if(map.GetTile(new TilePosition(x,y))!=TileKind.Corridor||corridorRandom.NextDouble()>.10)continue;
                Vector2 center=new Vector2(x+.5f,.5f-y);
                string wall=!Floor(center+Vector2.up)?"N":!Floor(center+Vector2.down)?"S":!Floor(center+Vector2.left)?"W":!Floor(center+Vector2.right)?"E":null;
                if(wall==null)continue;
                bool clear=true;
                foreach(var room in map.Rooms)if(Gap(RoomBounds(room),center)<2.5f){clear=false;break;}
                foreach(var prop in Items)if(Vector2.Distance(prop.transform.position,center)<7){clear=false;break;}
                foreach(var chest in spawns.Chests)if(Vector2.Distance(DungeonView.WorldPosition(chest.Position),center)<2)clear=false;
                foreach(var enemy in spawns.Enemies)if(Vector2.Distance(DungeonView.WorldPosition(enemy.Position),center)<1)clear=false;
                if(!clear||Items.Count>=512)continue;
                var data=candidates[corridorRandom.Next(candidates.Count)];
                Vector2 point=FlushRoot(data,center,new Rect(x,-y,1,1),wall);
                Items.Add(Spawn(data,100000+sequence++,-1,wall,point));
            }
        }
        public bool TryPlace(PropDefinition data,int roomId,string wall,Vector2 initial,int id)
        {
            if(Items.Count>=512)return false;
            if(data.corner.Length>0?wall!=data.corner:Array.IndexOf(data.walls,wall)<0)return false;
            Rect room=RoomBounds(map.Rooms[roomId]);Vector2 point=FlushRoot(data,initial,room,wall);Rect visual=data.Bounds(point);
            Rect footprint=Rect.MinMaxRect(Mathf.Max(room.xMin,visual.xMin),Mathf.Max(room.yMin,visual.yMin),Mathf.Min(room.xMax,visual.xMax),Mathf.Min(room.yMax,visual.yMax));
            if(!WallSupported(data,visual,room,wall)||!ReservedClear(footprint,roomId))return false;
            foreach(var old in Items)if(old.VisualBounds.Overlaps(visual))return false;
            if(data.corner.Length>0)
            {
                // A true 2x2 inner corner, with both complete outer wall segments.
                float x=wall.Contains("W")?room.xMin:room.xMax-2,y=wall.Contains("N")?room.yMax-2:room.yMin;
                for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++)if(!Floor(new Vector2(x+dx+.5f,y+dy+.5f)))return false;
            }
            var prop=Spawn(data,id,roomId,wall,point);
            Physics2D.SyncTransforms();
            if(!ConnectedRoom(roomId)) {prop.gameObject.SetActive(false);Destroy(prop.gameObject);Physics2D.SyncTransforms();return false;}
            if(data.components!=null&&data.components.Length>0)
            {
                prop.gameObject.SetActive(false);Destroy(prop.gameObject);
                for(int c=0;c<data.components.Length;c++)
                {var part=data.components[c];Items.Add(Spawn(catalog.Get(part.id),1000000+id*10+c,roomId,wall,point+new Vector2(part.offset[0],part.offset[1])/32));}
            }
            else Items.Add(prop);
            return true;
        }
        private DungeonProp Spawn(PropDefinition data,int id,int roomId,string wall,Vector2 point)
        {
            var obj=new GameObject("Prop "+id+" "+data.name);obj.transform.SetParent(transform,false);
            var prop=obj.AddComponent<DungeonProp>();prop.Initialize(this,data,id,roomId,wall,point,roomId>=0?RoomBounds(map.Rooms[roomId]):new Rect(point-Vector2.one,Vector2.one*2));return prop;
        }
        private bool WallSupported(PropDefinition data,Rect visual,Rect room,string wall)
        {
            if(visual.xMin<room.xMin-.19f||visual.xMax>room.xMax+.19f||visual.yMin<room.yMin-.19f||visual.yMax>room.yMax+(data.tall?2:.19f))return false;
            if(wall.Contains("N")||wall.Contains("S"))
            {
                float y=wall.Contains("N")?room.yMax+.05f:room.yMin-.05f;
                for(float x=Mathf.Max(room.xMin+.04f,visual.xMin);x<Mathf.Min(room.xMax-.04f,visual.xMax);x+=.25f)if(Floor(new Vector2(x,y)))return false;
            }
            if(wall.Contains("E")||wall.Contains("W"))
            {
                float x=wall.Contains("W")?room.xMin-.05f:room.xMax+.05f;
                for(float y=Mathf.Max(room.yMin+.04f,visual.yMin);y<Mathf.Min(room.yMax-.04f,visual.yMax);y+=.25f)if(Floor(new Vector2(x,y)))return false;
            }
            if(data.tall)for(float y=room.yMax+.05f;y<visual.yMax;y+=.25f)if(Floor(new Vector2(visual.center.x,y)))return false;
            return true;
        }
        private bool Floor(Vector2 point)=>map.IsTileFloor(new TilePosition(Mathf.FloorToInt(point.x),Mathf.FloorToInt(1-point.y)));
        private static float Gap(Rect rect,Vector2 point)=>Vector2.Distance(point,new Vector2(Mathf.Clamp(point.x,rect.xMin,rect.xMax),Mathf.Clamp(point.y,rect.yMin,rect.yMax)));
        private bool ReservedClear(Rect rect,int roomId)
        {
            var room=map.Rooms[roomId];Rect bounds=RoomBounds(room);
            // Preserve two entire centre lanes for combat and navigation.
            if(rect.Overlaps(new Rect(bounds.center.x-1,bounds.yMin+2,2,Mathf.Max(0,bounds.height-4)))||rect.Overlaps(new Rect(bounds.xMin+2,bounds.center.y-1,Mathf.Max(0,bounds.width-4),2)))return false;
            foreach(var opening in room.Openings)
                if(Gap(rect,new Vector2(opening.X*3+1.5f,1-(opening.Y*3+1.5f)))<3.5f)return false;
            foreach(var chest in spawns.Chests)if(Gap(rect,DungeonView.WorldPosition(chest.Position))<1.7f)return false;
            foreach(var enemy in spawns.Enemies)if(Gap(rect,DungeonView.WorldPosition(enemy.Position))<.8f)return false;
            foreach(var landmark in player.transform.parent.GetComponentsInChildren<DungeonFountain>())if(Gap(rect,landmark.transform.position)<3.4f)return false;
            foreach(var sprite in player.transform.parent.GetComponentsInChildren<SpriteRenderer>())
                if(sprite.name=="object.entrance"&&Gap(rect,sprite.transform.position)<4||sprite.name.StartsWith("object.exit")&&Gap(rect,sprite.transform.position)<2||sprite.name=="object.fountain"&&Gap(rect,sprite.transform.position)<3.4f)return false;
            return true;
        }
        public bool ConnectedRoom(int roomId)
        {
            var room=map.Rooms[roomId];int w=room.TileWidth,h=room.TileHeight;var open=new Queue<Vector2Int>();var seen=new bool[w*h];var walk=new bool[w*h];int total=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                Vector2 point=new Vector2(room.TileLeft+x+.5f,.5f-room.TileTop-y);
                if(!DungeonCombatPhysics.FreeFeet(point,.32f))continue;
                walk[y*w+x]=true;total++;if(open.Count==0){open.Enqueue(new Vector2Int(x,y));seen[y*w+x]=true;}
            }
            int reached=0;Vector2Int[] directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(open.Count>0){var p=open.Dequeue();reached++;foreach(var d in directions){var n=p+d;if(n.x<0||n.y<0||n.x>=w||n.y>=h)continue;int i=n.y*w+n.x;if(!walk[i]||seen[i])continue;seen[i]=true;open.Enqueue(n);}}
            return total>0&&reached==total;
        }
        internal SpriteRenderer Render(Transform parent,Sprite sprite,Vector2 offset,bool ground,Vector2 anchor)
        {
            var obj=new GameObject(sprite!=null?sprite.name:"Remnant",typeof(SpriteRenderer));obj.transform.SetParent(parent,false);obj.transform.localPosition=offset;
            var renderer=obj.GetComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;renderer.sortingOrder=ground?2:DungeonDepthSort.OrderForY(anchor.y);
            var properties=new MaterialPropertyBlock();properties.SetVector("_GroundLightPosition",new Vector4(anchor.x,anchor.y,0,1));DungeonSpriteGrid.Apply(renderer,properties);
            parent.GetComponentInParent<DungeonProp>()?.renderers.Add(renderer);
            return renderer;
        }
        public void Melee(Vector2 origin,Vector2 direction,float damage)
        {
            int attack=NewAttackId();foreach(var prop in Items)
            {
                if(prop==null||!prop.HitPoint(origin,out var point))continue;Vector2 delta=point-origin;
                if(delta.sqrMagnitude>1.65f*1.65f||Vector2.Dot(delta.normalized,direction)<.52f)continue;
                if(DungeonCombatPhysics.ClearLine(origin,point,0,null,prop.transform))prop.ReceiveHit(damage,attack);
            }
        }
        public void AreaHit(Vector2 center,float radius,float damage)
        {int attack=NewAttackId();foreach(var prop in Items)if(prop.HitPoint(center,out var point)&&(point-center).sqrMagnitude<=radius*radius&&DungeonCombatPhysics.ClearLine(center,point,0,null,prop.transform))prop.ReceiveHit(damage,attack);}
        public void ObstaclesChanged(){dirty=true;}
        public PropCheckpoint[] Snapshot(){var saved=new PropCheckpoint[Items.Count];for(int i=0;i<Items.Count;i++)saved[i]=Items[i].Snapshot();return saved;}
        public void Restore(PropCheckpoint[] saved)
        {
            if(saved==null)return;foreach(var old in Items){old.gameObject.SetActive(false);Destroy(old.gameObject);}Items.Clear();
            foreach(var item in saved){if(item.room< -1||item.room>=map.Rooms.Count)continue;var data=catalog.Get(item.asset);if(data==null)continue;var prop=Spawn(data,item.id,item.room,item.wall,item.position);Items.Add(prop);prop.Restore(item);}
            Physics2D.SyncTransforms();RebuildShadowGeometry();DungeonCombatNavigation.Invalidate(map);
        }
        private void OnEnable(){if(map!=null)Current=this;}
        private void OnDisable(){if(Current==this)Current=null;}
    }
}
