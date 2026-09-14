using System;
using UnityEngine;
using SunsetGlow.EnvironmentArt;

namespace SunsetGlow.Player
{
    public struct FootContactEvent
    {
        public bool left, landing;
        public SurfaceContact surface;
        public float speed, intensity;
        public int sequence;
    }

    public sealed class FootContactEmitter : MonoBehaviour
    {
        public FirstPersonController player;
        public SurfaceQuery surfaces;
        public bool useAnimationEvents;
        [Min(.2f)] public float stepLength=.68f;
        public event Action<FootContactEvent> Contact;
        public int EmittedCount { get; private set; }
        public FootContactEvent LastContact { get; private set; }
        float travelled;
        bool nextLeft=true;
        int lastAnimationStamp=-1;

        void OnEnable() { if(player!=null) {player.Moved+=OnMoved;player.Teleported+=OnTeleport;} }
        void OnDisable() { if(player!=null) {player.Moved-=OnMoved;player.Teleported-=OnTeleport;} }
        void OnTeleport(){travelled=0;lastAnimationStamp=-1;}
        void OnMoved(Vector3 before,Vector3 after,float seconds)
        {
            if(useAnimationEvents||player.Paused)return;
            if(!player.IsGrounded){travelled=0;return;}
            float distance=new Vector2(after.x-before.x,after.z-before.z).magnitude;
            if(distance<.001f)return;
            if(distance>3f) {travelled=0;return;} // A discontinuity is not a footstep.
            if(!surfaces.TryContact(after,out _)){travelled=0;return;}
            travelled+=distance;
            float stride=stepLength*Mathf.Lerp(1,1.25f,Mathf.InverseLerp(4,6,distance/Mathf.Max(.001f,seconds)));
            if(travelled<stride)return;
            travelled=Mathf.Min(travelled-stride,stride*.99f);
            Emit(nextLeft,distance/Mathf.Max(.001f,seconds),false);
        }
        // A future bound animation calls this path instead of distance fallback, never both.
        public void AnimationContact(bool left,int animationStamp,float speed)
        {
            if(!useAnimationEvents||animationStamp==lastAnimationStamp||player.Paused||!player.IsGrounded)return;
            lastAnimationStamp=animationStamp;
            if(speed>.05f)Emit(left,speed,false);
        }
        void Emit(bool left,float speed,bool landing)
        {
            Vector3 foot=player.transform.position+player.transform.right*(left?-.105f:.105f);
            if(!surfaces.TryContact(foot,out var surface))return;
            LastContact=new FootContactEvent{left=left,landing=landing,surface=surface,speed=speed,intensity=Mathf.Clamp01(speed/6),sequence=++EmittedCount};
            nextLeft=!left;Contact?.Invoke(LastContact);
        }
    }
}
