using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Moving pixel motes follow real floor routes, never a line through walls.</summary>
    public sealed class DungeonTrace : MonoBehaviour
    {
        private DungeonSpellcaster caster;
        private DungeonCombatNavigation enemyNavigation, exitNavigation;
        private readonly List<Vector2> enemyPath = new List<Vector2>(), exitPath = new List<Vector2>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color32> colors = new List<Color32>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> indices = new List<int>();
        private Mesh mesh;
        private float age, refresh;
        private DungeonSpellIcon enemyIcon, exitIcon;
        public static void Create(DungeonSpellcaster caster)
        {
            var obj = new GameObject("Trace paths", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(caster.transform.parent, false);
            var trace = obj.AddComponent<DungeonTrace>(); trace.caster = caster;
            trace.enemyNavigation = DungeonCombatNavigation.Independent(caster.Map);
            trace.exitNavigation = DungeonCombatNavigation.Independent(caster.Map);
            trace.mesh = new Mesh {name="Trace pixel particles"}; trace.mesh.MarkDynamic();
            obj.GetComponent<MeshFilter>().sharedMesh = trace.mesh;
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = caster.Owner.EffectMaterial; renderer.sortingOrder = 3;
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_CombatMesh",1); properties.SetFloat("_EffectUnlit",1);
            properties.SetTexture("_MainTex",Texture2D.whiteTexture); renderer.SetPropertyBlock(properties);
            trace.enemyIcon = DungeonSpellIcon.Create(obj.transform,"skull",caster.Owner.EffectMaterial,new Color(1,.3f,.3f));
            trace.exitIcon = DungeonSpellIcon.Create(obj.transform,"stairs",caster.Owner.EffectMaterial,new Color(.3f,.7f,1));
        }
        private void Update()
        {
            if (caster == null || caster.Owner.IsDead) { Destroy(gameObject); return; }
            if (!caster.Owner.CanAct) return;
            age += Time.deltaTime; refresh -= Time.deltaTime;
            if (age >= 3) { Destroy(gameObject); return; }
            Vector2 origin = caster.transform.position;
            if (refresh <= 0)
            {
                refresh = .3f;
                DungeonEnemy nearest = null; float best = float.PositiveInfinity;
                foreach (DungeonEnemy enemy in DungeonEnemy.Active)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    float gap = ((Vector2)enemy.transform.position-origin).sqrMagnitude;
                    if (gap < best) { best=gap; nearest=enemy; }
                }
                enemyPath.Clear(); exitPath.Clear();
                if (nearest != null) BuildPath(enemyNavigation,origin,nearest.transform.position,enemyPath);
                if (caster.Map?.ExitTile != null) BuildPath(exitNavigation,origin,DungeonView.WorldPosition(caster.Map.ExitTile.Value),exitPath);
            }
            vertices.Clear(); colors.Clear(); uv.Clear(); indices.Clear();
            DrawPath(enemyPath,new Color(1,.22f,.22f)); DrawPath(exitPath,new Color(.2f,.6f,1));
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            PlaceIcon(enemyIcon,enemyPath,origin); PlaceIcon(exitIcon,exitPath,origin);
        }
        private static void BuildPath(DungeonCombatNavigation navigation,Vector2 from,Vector2 to,List<Vector2> path)
        {
            if (navigation != null) navigation.Path(from,to,path);
            else if (DungeonCombatPhysics.ClearLine(from,to)) { path.Add(from); path.Add(to); }
        }
        private static void PlaceIcon(DungeonSpellIcon icon,List<Vector2> path,Vector2 origin)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(path.Count>1);
            if (path.Count<2) return;
            Vector2 heading=path[1]-origin;
            if (heading.sqrMagnitude<.04f && path.Count>2) heading=path[2]-origin;
            icon.Place(origin+Vector2.up*.5f+heading.normalized*.85f,origin.y);
        }
        private void DrawPath(List<Vector2> path,Color color)
        {
            color.a=Mathf.Min(1,(3-age)/.4f);
            float next=(age*1.5f)%.35f;
            for (int segment=1;segment<path.Count && vertices.Count<16000;segment++)
            {
                Vector2 delta=path[segment]-path[segment-1]; float length=delta.magnitude;
                if(length<.001f)continue;
                for (;next<length && vertices.Count<16000;next+=.35f)
                {
                    Vector2 p=DungeonCombatPixels.Snap(path[segment-1]+delta*(next/length));
                    int n=vertices.Count; const float size=2f/32;
                    vertices.Add(p);vertices.Add(p+Vector2.up*size);vertices.Add(p+Vector2.one*size);vertices.Add(p+Vector2.right*size);
                    Color32 tint=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
                    for(int i=0;i<4;i++){ colors.Add(tint);uv.Add(Vector2.one*.5f); }
                    indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n+2);indices.Add(n+3);indices.Add(n);
                }
                next-=length;
            }
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
