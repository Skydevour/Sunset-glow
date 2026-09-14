using UnityEngine;
using SunsetGlow.EnvironmentArt;
namespace SunsetGlow.Player
{
    public sealed class FootContactWaterFeedback:MonoBehaviour
    {
        public FootContactEmitter feet;
        public WaterContactSystem water;
        public FirstPersonController player;
        public int WaterFootsteps { get; private set; }
        void OnEnable(){if(feet!=null)feet.Contact+=OnContact;}
        void OnDisable(){if(feet!=null)feet.Contact-=OnContact;}
        void OnContact(FootContactEvent contact)
        {
            if(player.Paused||contact.surface.kind!=SurfaceKind.Water)return;
            WaterFootsteps++;
            water.EmitRipple(contact.surface.point,Mathf.Lerp(.6f,1.5f,contact.intensity));
        }
    }
}
