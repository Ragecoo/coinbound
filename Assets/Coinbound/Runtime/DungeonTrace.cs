using System.Collections.Generic;
using UnityEngine;

namespace Coinbound
{
    /// <summary>Free-flowing pixel wisps follow smoothed, wall-safe routes rather than tile-center steps.</summary>
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
        private DungeonEnemy trackedEnemy;
        private Vector2 enemyOrigin, exitOrigin, enemyDestination;
        private bool enemyCached, exitCached;
        private readonly List<Vector2> corners = new List<Vector2>(128);
        private const float VisibleRouteLength = 20;
        public double MaximumUpdateMilliseconds { get; private set; }
        public double TotalUpdateMilliseconds { get; private set; }
        public int MeasuredFrames { get; private set; }
        public static void Create(DungeonSpellcaster caster)
        {
            var obj = new GameObject("Trace paths", typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(caster.transform.parent, false);
            var trace = obj.AddComponent<DungeonTrace>(); trace.caster = caster;
            caster.Track(obj);
            trace.enemyNavigation = DungeonCombatNavigation.Independent(caster.Map);
            trace.exitNavigation = DungeonCombatNavigation.Independent(caster.Map);
            trace.mesh = new Mesh {name="Trace pixel particles"}; trace.mesh.MarkDynamic();
            obj.GetComponent<MeshFilter>().sharedMesh = trace.mesh;
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = caster.Owner.EffectMaterial; renderer.sortingOrder = 3;
            var properties = new MaterialPropertyBlock(); properties.SetFloat("_CombatMesh",1); properties.SetFloat("_EffectUnlit",1);
            properties.SetTexture("_MainTex",Texture2D.whiteTexture); renderer.SetPropertyBlock(properties);
            trace.enemyIcon = DungeonSpellIcon.Create(obj.transform,"sword",caster.Owner.EffectMaterial,new Color(1,.3f,.3f));
            trace.exitIcon = DungeonSpellIcon.Create(obj.transform,"stairs",caster.Owner.EffectMaterial,new Color(.3f,.7f,1));
            if (trace.enemyIcon != null) trace.enemyIcon.gameObject.SetActive(false);
            if (trace.exitIcon != null) trace.exitIcon.gameObject.SetActive(false);
        }
        private void LateUpdate()
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            UpdateTrace();
            double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            MaximumUpdateMilliseconds = System.Math.Max(MaximumUpdateMilliseconds,elapsed);
            TotalUpdateMilliseconds += elapsed; MeasuredFrames++;
        }
        private void UpdateTrace()
        {
            if (caster == null || caster.Owner.IsDead) { Destroy(gameObject); return; }
            if (!caster.Owner.CanAct) return;
            age += Time.deltaTime; refresh -= Time.deltaTime;
            if (!caster.IsActive(DungeonSpell.Trace)) { Destroy(gameObject); return; }
            Vector2 origin = caster.transform.position;
            if (enemyPath.Count > 0 && (trackedEnemy == null || trackedEnemy.IsDead || !trackedEnemy.isActiveAndEnabled))
            {
                trackedEnemy = null; enemyPath.Clear(); refresh = 0; enemyCached = false;
                if (enemyIcon != null) enemyIcon.gameObject.SetActive(false);
            }
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
                if (nearest == null) { enemyPath.Clear(); enemyCached = false; }
                else if (!enemyCached || nearest != trackedEnemy || (origin-enemyOrigin).sqrMagnitude > .25f || ((Vector2)nearest.transform.position-enemyDestination).sqrMagnitude > .25f)
                {
                    enemyOrigin = origin; enemyDestination = nearest.transform.position;
                    enemyPath.Clear(); BuildPath(enemyNavigation,origin,enemyDestination,enemyPath); enemyCached = true;
                }
                trackedEnemy = nearest;
                if (caster.Map?.ExitTile != null && (!exitCached || (origin-exitOrigin).sqrMagnitude > .25f))
                {
                    exitOrigin = origin; exitPath.Clear();
                    BuildPath(exitNavigation,origin,DungeonView.WorldPosition(caster.Map.ExitTile.Value),exitPath); exitCached = true;
                }
            }
            vertices.Clear(); colors.Clear(); uv.Clear(); indices.Clear();
            DrawPath(enemyPath,new Color(1,.28f,.22f), 0); DrawPath(exitPath,new Color(.3f,.7f,1), .275f);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0,uv); mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
            Vector2 enemyHeading = Heading(enemyPath, origin), exitHeading = Heading(exitPath, origin);
            bool shared = enemyHeading.sqrMagnitude > .01f && exitHeading.sqrMagnitude > .01f && Vector2.Dot(enemyHeading,exitHeading) > .85f;
            Vector2 spread = shared ? new Vector2(-enemyHeading.y,enemyHeading.x) * .17f : Vector2.zero;
            PlaceIcon(enemyIcon,enemyPath,origin,spread); PlaceIcon(exitIcon,exitPath,origin,-spread);
        }
        private void BuildPath(DungeonCombatNavigation navigation,Vector2 from,Vector2 to,List<Vector2> path)
        {
            if (navigation != null) navigation.Path(from,to,path,VisibleRouteLength + 2);
            else if (DungeonCombatPhysics.ClearLine(from,to)) { path.Add(from); path.Add(to); }
            // Remove visible tile-center waypoints, then round bends in continuous world coordinates.
            for (int i=0;i<path.Count-2;i++)
            {
                int far=Mathf.Min(path.Count-1,i+8);
                while(far>i+1 && !DungeonCombatPhysics.ClearLine(path[i],path[far],.2f)) far--;
                if(far>i+1) path.RemoveRange(i+1,far-i-1);
            }
            if(path.Count<3)return;
            corners.Clear(); corners.AddRange(path); path.Clear();path.Add(corners[0]);
            for(int i=0;i<corners.Count-1;i++)
            {
                Vector2 p0=corners[Mathf.Max(0,i-1)],p1=corners[i],p2=corners[i+1],p3=corners[Mathf.Min(corners.Count-1,i+2)];
                int steps=Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(p1,p2)/.3f),2,32);
                for(int step=1;step<=steps;step++)
                {
                    float t=(float)step/steps;
                    Vector2 straight=Vector2.Lerp(p1,p2,t);
                    Vector2 curve=.5f*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t);
                    bool safe=DungeonCombatPhysics.FreeFeet(curve,.16f) && DungeonCombatPhysics.ClearLine(path[path.Count-1],curve,.12f);
                    path.Add(safe?curve:straight);
                }
            }
        }
        private static Vector2 Heading(List<Vector2> path, Vector2 origin)
        {
            for (int i=1;i<path.Count;i++) if ((path[i]-origin).sqrMagnitude >= .04f) return (path[i]-origin).normalized;
            return Vector2.zero;
        }
        private static void PlaceIcon(DungeonSpellIcon icon,List<Vector2> path,Vector2 origin,Vector2 offset)
        {
            if (icon == null) return;
            icon.gameObject.SetActive(path.Count>1);
            if (path.Count<2) return;
            Vector2 heading=Heading(path,origin);
            icon.gameObject.SetActive(heading.sqrMagnitude > .01f);
            icon.Place(origin+Vector2.up*.5f+heading*.85f+offset,origin.y);
            if (heading.sqrMagnitude > .01f)
                DungeonEffectLights.Glow(icon, origin + heading * .35f, 1.35f, .55f, icon.GetComponent<SpriteRenderer>().color);
        }
        private void DrawPath(List<Vector2> path,Color color,float phase)
        {
            color.a=Mathf.Min(1,caster.ActiveRemaining(DungeonSpell.Trace)/.4f);
            const float spacing = .55f;
            float flow = age * .7f + phase;
            float next = flow % spacing;
            int cluster = -Mathf.FloorToInt(flow / spacing);
            float traveled=0;
            for (int segment=1;segment<path.Count && traveled < VisibleRouteLength;segment++)
            {
                Vector2 delta=path[segment]-path[segment-1]; float length=delta.magnitude;
                if(length<.001f)continue;
                for (;next<length && traveled+next < VisibleRouteLength;next+=spacing,cluster++)
                {
                    Vector2 basis=path[segment-1]+delta*(next/length);
                    Vector2 direction=delta/length,side=new Vector2(-direction.y,direction.x);
                    // Three irregular grains replace the five-quad comet, preserving the mesh budget.
                    for(int grain=0;grain<3;grain++)
                    {
                        float seed = DustNoise(cluster,grain,0);
                        // Keep a visible spine on the route; companion grains form a narrow dusty cluster.
                        float scatter = grain == 0 ? 0 : (grain == 1 ? -1 : 1)*(.035f+DustNoise(cluster,grain,1)*.07f);
                        float along = grain == 0 ? 0 : (DustNoise(cluster,grain,2)-.5f)*.3f;
                        float motion = DungeonSettings.ReducedMotion ? 0 : Mathf.Sin(age*2.5f+seed*12)*.025f;
                        Vector2 point = basis + side*(scatter+(grain == 0 ? 0 : motion)) + direction*along;
                        if(!Floor(point))continue;
                        float shimmer = .5f+.5f*Mathf.Sin(age*(3+seed*2)+seed*24);
                        float brightness = Mathf.Ceil((.75f+shimmer*.25f)*4)/4;
                        Color tint = Color.Lerp(color, new Color(1,1,1,color.a), grain == 0 ? .22f : .06f);
                        tint.r *= brightness; tint.g *= brightness; tint.b *= brightness;
                        tint.a *= .85f + shimmer*.15f;
                        float size = grain == 0 || seed > .55f ? 2f/32 : 1f/32;
                        AddPixel(DungeonCombatPixels.Snap(point),tint,size);
                    }
                }
                next-=length;
                traveled+=length;
            }
        }
        private static float DustNoise(int cluster,int grain,int channel)
        {
            uint value = unchecked((uint)cluster*747796405u + (uint)grain*2891336453u + (uint)channel*277803737u);
            value = unchecked((value ^ (value >> 16))*2246822519u);
            return (value & 65535)/65535f;
        }
        private bool Floor(Vector2 point) => caster.Map == null || caster.Map.IsTileFloor(
            new ProceduralGeneration.TilePosition(Mathf.FloorToInt(point.x),Mathf.FloorToInt(1-point.y)));
        private void AddPixel(Vector2 point, Color color, float size)
        {
            int n=vertices.Count;
            vertices.Add(point);vertices.Add(point+Vector2.up*size);vertices.Add(point+Vector2.one*size);vertices.Add(point+Vector2.right*size);
            Color32 tint=QualitySettings.activeColorSpace==ColorSpace.Linear?color.linear:color;
            for(int i=0;i<4;i++){colors.Add(tint);uv.Add(Vector2.one*.5f);}
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n+2);indices.Add(n+3);indices.Add(n);
        }
        private void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
