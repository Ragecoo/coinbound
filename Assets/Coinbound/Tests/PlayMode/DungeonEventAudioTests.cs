using System;
using NUnit.Framework;
using UnityEngine;
namespace Coinbound.Tests
{
    public sealed class DungeonEventAudioTests
    {
        [Test] public void CuesHaveSilentEndpointsAndHeadroom()
        {
            foreach(DungeonEventAudio.Cue cue in Enum.GetValues(typeof(DungeonEventAudio.Cue)))
            {
                var data=DungeonEventAudio.Samples(cue);float peak=0;
                Assert.AreEqual(0,data[0]);Assert.AreEqual(0,data[data.Length-1]);
                for(int i=0;i<data.Length;i++){Assert.IsFalse(float.IsNaN(data[i]));peak=Mathf.Max(peak,Mathf.Abs(data[i]));}
                Assert.That(peak,Is.InRange(.001f,.3f),cue.ToString());
            }
        }
        [Test] public void WaterLoopContinuesWaveformAtJoin()
        {
            var source=new float[44100];for(int i=0;i<source.Length;i++)source[i]=Mathf.Sin(i*.01f)*.2f;
            var loop=DungeonAudio.SeamlessWater(source);
            Assert.That(Mathf.Abs(loop[0]-loop[loop.Length-1]),Is.LessThan(.003f));
            Assert.That(loop.Length,Is.EqualTo(33075));
        }
    }
}
