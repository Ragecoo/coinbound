using NUnit.Framework;
using UnityEngine;
namespace Coinbound.Tests
{
    public sealed class DungeonCoinGestureTests
    {
        private static Vector3[] Fist()
        {
            var p=new Vector3[21];
            for(int i=0;i<4;i++)
            {
                int m=5+i*4;float x=(i-1)*.025f;
                p[m]=new Vector3(x,-.06f,0);p[m+1]=new Vector3(x,-.09f,0);
                p[m+2]=new Vector3(x,-.065f,.01f);p[m+3]=new Vector3(x,-.05f,.01f);
            }
            p[1]=new Vector3(-.04f,-.01f,0);p[2]=new Vector3(-.04f,-.02f,0);
            p[3]=new Vector3(-.03f,-.05f,0);p[4]=new Vector3(-.02f,-.06f,0);return p;
        }
        private static Vector3[] Image(Vector3[] world,float lift=0)
        {var result=new Vector3[21];for(int i=0;i<21;i++)result[i]=new Vector3(world[i].x*4+.5f,world[i].y*4+.7f-lift,0);return result;}
        [TestCase(-.11f)]
        [TestCase(-.081f)]
        public void FistThenThumbsUpWithoutWristMotionTriggersExactlyOnce(float thumbY)
        {
            var gesture=new DungeonCoinGesture();var world=Fist();
            for(int i=0;i<11;i++)Assert.That(gesture.Observe(Image(world),world,.05f),Is.False);
            Assert.That(gesture.Stage,Is.EqualTo(2));
            world[3]=new Vector3(-.04f,-.055f,0);world[4]=new Vector3(-.04f,thumbY,0);
            for(int i=0;i<2;i++)Assert.That(gesture.Observe(Image(world),world,.05f),Is.False);
            Assert.That(gesture.Observe(Image(world),world,.05f),Is.True);
            Assert.That(gesture.Observe(Image(world,.025f),world,.05f),Is.False);
        }
        [Test] public void ThumbsUpAloneOpenHandAndLostTrackCannotToss()
        {
            var gesture=new DungeonCoinGesture();var fist=Fist();var like=Fist();
            like[3]=new Vector3(-.04f,-.055f,0);like[4]=new Vector3(-.04f,-.11f,0);
            for(int i=0;i<40;i++)Assert.That(gesture.Observe(Image(like),like,.05f),Is.False);
            for(int i=0;i<11;i++)gesture.Observe(Image(fist),fist,.05f);
            for(int i=0;i<20;i++)gesture.Observe(null,null,.05f);
            for(int i=0;i<20;i++)Assert.That(gesture.Observe(Image(like),like,.05f),Is.False);
            gesture.Reset();for(int i=0;i<11;i++)gesture.Observe(Image(fist),fist,.05f);
            var open=Fist();for(int f=0;f<4;f++){int m=5+f*4;open[m+3]=open[m]+new Vector3(0,-.12f,0);}
            Assert.That(gesture.Observe(Image(open),open,.05f),Is.False);
            Assert.That(gesture.Stage,Is.Zero);
        }
        [Test] public void WholeHandMotionAndBriefPoseCannotToss()
        {
            var gesture=new DungeonCoinGesture();var world=Fist();
            for(int i=0;i<11;i++)gesture.Observe(Image(world),world,.05f);
            Assert.That(gesture.Observe(Image(world,.07f),world,.05f),Is.False);
            gesture.Reset();gesture.Observe(Image(world),world,.05f);
            world[3]=new Vector3(-.04f,-.055f,0);world[4]=new Vector3(-.04f,-.11f,0);
            Assert.That(gesture.Observe(Image(world,.025f),world,.05f),Is.False);
            for(int i=0;i<20;i++)Assert.That(gesture.Observe(null,null,.05f),Is.False);Assert.That(gesture.Stage,Is.Zero);
        }
    }
}
