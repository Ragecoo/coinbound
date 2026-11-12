using System;
using UnityEngine;
namespace Coinbound
{
    [Serializable] public sealed class PropBox { public float x,y,w,h; public Rect Local(int[] pivot)=>new Rect((x-pivot[0])/32,(y-pivot[1])/32,w/32,h/32); }
    [Serializable] public sealed class PropRemnant { public string file; public int id; public int[] offset; public bool solid; }
    [Serializable] public sealed class PropDefinition
    {
        public int id,width,height,floor;public string resource;public bool placeable,overlay;public PropRemnant[] components;public string name,file,material,extra,corner;public int[] pivot,bounds,contacts;
        public bool solid,tall,ground;public float hp;public PropBox[] boxes,hitboxes;
        public string[] themes,walls;public PropRemnant[] debris,survivors;public Vector2[] flames;
        [NonSerialized] private Sprite sprite;
        public Sprite Sprite=>sprite!=null?sprite:sprite=Resources.Load<Sprite>(string.IsNullOrEmpty(resource)?"EnvironmentProps52V5/sprites/"+file:resource);
        public Rect Bounds(Vector2 origin)=>new Rect(origin.x+bounds[0]/32f,origin.y+bounds[1]/32f,(bounds[2]-bounds[0])/32f,(bounds[3]-bounds[1])/32f);
    }
    [Serializable] public sealed class PropRecipeMember {public int asset_id,tangent_cell;}
    [Serializable] public sealed class PropRecipe {public string id,theme;public string[] allowed_walls;public PropRecipeMember[] members;}
    [Serializable] public sealed class PropRecipes {public PropRecipe[] recipes;}
    [Serializable] public sealed class DungeonPropCatalog
    {
        public PropDefinition[] assets;
        private static DungeonPropCatalog cached,themed;
        public static DungeonPropCatalog ForFloor(int floor)=>floor==1?Current:themed??(themed=JsonUtility.FromJson<DungeonPropCatalog>(Resources.Load<TextAsset>("FloorDecor/catalog").text));
        public static DungeonPropCatalog Current=>cached??(cached=JsonUtility.FromJson<DungeonPropCatalog>(Resources.Load<TextAsset>("EnvironmentProps52V5/catalog").text));
        public PropDefinition Get(int id)=>id>0&&id<=assets.Length?assets[id-1]:null;
    }
}
