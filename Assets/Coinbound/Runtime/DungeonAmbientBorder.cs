using UnityEngine;
using UnityEngine.UI;
namespace Coinbound
{
    public sealed class DungeonAmbientBorder : RawImage
    {
        protected override void Awake()
        {
            base.Awake();
            texture = Resources.Load<Texture2D>("InterfaceArt/Vignette/vignette-ambient");
            raycastTarget = false;
        }
        protected override void OnPopulateMesh(VertexHelper mesh) => DungeonDamageBorder.PopulatePixelVignette(this, mesh);
    }
}
