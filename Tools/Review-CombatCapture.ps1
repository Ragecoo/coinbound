param(
    [Parameter(Mandatory = $true)][string]$CaptureDirectory,
    [string]$OutputDirectory,
    [string]$Only = '*',
    [ValidateRange(192, 640)][int]$CropSize = 384,
    [ValidateRange(0.25, 2.0)][double]$PlaybackSpeed = 1.0
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class CoinboundCombatReview
{
    private sealed class Bin
    {
        public long Count, R, G, B;
        public Color Color { get { return Color.FromArgb((int)(R / Count), (int)(G / Count), (int)(B / Count)); } }
    }
    private sealed class Box
    {
        public List<Bin> Bins;
        public long Count;
        public int Channel, Range;
        public Box(List<Bin> bins)
        {
            Bins = bins;
            int[] lo = {255,255,255}, hi = {0,0,0};
            foreach (Bin bin in bins)
            {
                Color color = bin.Color;
                int[] rgb = {color.R,color.G,color.B};
                for (int i = 0; i < 3; i++) { lo[i] = Math.Min(lo[i],rgb[i]); hi[i] = Math.Max(hi[i],rgb[i]); }
                Count += bin.Count;
            }
            for (int i = 0; i < 3; i++) if (hi[i] - lo[i] > Range) { Range = hi[i] - lo[i]; Channel = i; }
        }
        public Color Mean()
        {
            long r=0,g=0,b=0;
            foreach (Bin bin in Bins) { r += bin.R; g += bin.G; b += bin.B; }
            return Color.FromArgb((int)(r/Count),(int)(g/Count),(int)(b/Count));
        }
    }

    private static Bitmap Crop(string path, int size)
    {
        using (var source = new Bitmap(path))
        {
            if (source.Width != 1280 || source.Height != 720) throw new InvalidOperationException("Expected 1280x720 runtime frame: " + path);
            int left = (source.Width - size) / 2;
            int top = Math.Max(0, Math.Min(source.Height-size, 352-size/2));
            var result = new Bitmap(size, size, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(result))
                graphics.DrawImageUnscaled(source, -left, -top);
            return result;
        }
    }

    private static byte[] Pixels(Bitmap bitmap)
    {
        BitmapData data = bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte[] pixels = new byte[bitmap.Width * bitmap.Height * 3];
            for (int y = 0; y < bitmap.Height; y++)
                Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*bitmap.Width*3,bitmap.Width*3);
            return pixels;
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static List<Color> Palette(List<byte[]> frames)
    {
        var bins = new Dictionary<int,Bin>();
        foreach (byte[] pixels in frames)
        for (int i = 0; i < pixels.Length; i += 6)
        {
            int b=pixels[i],g=pixels[i+1],r=pixels[i+2];
            int key=((r>>3)<<10)|((g>>3)<<5)|(b>>3);
            Bin bin;
            if (!bins.TryGetValue(key,out bin)) { bin=new Bin(); bins.Add(key,bin); }
            bin.Count++; bin.R+=r; bin.G+=g; bin.B+=b;
        }
        var boxes = new List<Box> { new Box(new List<Bin>(bins.Values)) };
        while (boxes.Count < 256)
        {
            int split=-1; double score=0;
            for (int i=0;i<boxes.Count;i++)
            {
                Box box=boxes[i];
                double candidate=box.Range*Math.Log(box.Count+1);
                if (box.Bins.Count>1 && candidate>score) { score=candidate; split=i; }
            }
            if (split<0) break;
            Box old=boxes[split];
            int channel=old.Channel;
            old.Bins.Sort((a,b)=> Component(a.Color,channel).CompareTo(Component(b.Color,channel)));
            long half=old.Count/2,sum=0; int cut=0;
            do { sum+=old.Bins[cut++].Count; } while(cut<old.Bins.Count-1 && sum<half);
            boxes[split]=new Box(old.Bins.GetRange(0,cut));
            boxes.Add(new Box(old.Bins.GetRange(cut,old.Bins.Count-cut)));
        }
        var result=new List<Color>();
        foreach(Box box in boxes) result.Add(box.Mean());
        return result;
    }

    private static int Component(Color color,int channel) { return channel==0 ? color.R : channel==1 ? color.G : color.B; }

    private static byte[] Indexed(byte[] pixels,List<Color> palette,Dictionary<int,byte> cache)
    {
        byte[] result=new byte[pixels.Length/3];
        for(int i=0,p=0;i<pixels.Length;i+=3,p++)
        {
            int b=pixels[i],g=pixels[i+1],r=pixels[i+2],key=(r<<16)|(g<<8)|b;
            byte index;
            if(!cache.TryGetValue(key,out index))
            {
                int best=int.MaxValue;
                for(int c=0;c<palette.Count;c++)
                {
                    int dr=r-palette[c].R,dg=g-palette[c].G,db=b-palette[c].B;
                    int error=dr*dr*3+dg*dg*4+db*db*2;
                    if(error<best) { best=error; index=(byte)c; }
                }
                cache.Add(key,index);
            }
            result[p]=index;
        }
        return result;
    }

    private static byte[] Compress(byte[] pixels)
    {
        // Keep a compact LZW dictionary below the 9-bit boundary and reset before it grows.
        // This is a regular GIF stream; no external binaries or platform GIF encoder are required.
        var bytes=new List<byte>(); var dictionary=new Dictionary<int,int>();
        int bits=0,buffer=0,nextCode=258;
        Action<int> write=code=> { buffer|=code<<bits; bits+=9; while(bits>=8) { bytes.Add((byte)(buffer&255)); buffer>>=8; bits-=8; } };
        write(256);
        int prefix=pixels[0];
        for(int i=1;i<pixels.Length;i++)
        {
            int key=(prefix<<8)|pixels[i],found;
            if(dictionary.TryGetValue(key,out found)) { prefix=found; continue; }
            write(prefix);
            if(nextCode<510) dictionary.Add(key,nextCode++);
            else { write(256); dictionary.Clear(); nextCode=258; }
            prefix=pixels[i];
        }
        write(prefix); write(257);
        if(bits>0) bytes.Add((byte)(buffer&255));
        return bytes.ToArray();
    }

    public static int Review(string[] sources,string gifPath,string contactPath,string label,int size,double fps)
    {
        var frames=new List<byte[]>();
        foreach(string source in sources) using(Bitmap crop=Crop(source,size)) frames.Add(Pixels(crop));
        List<Color> palette=Palette(frames);
        var cache=new Dictionary<int,byte>();
        using(var writer=new BinaryWriter(File.Create(gifPath)))
        {
            writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
            writer.Write((ushort)size); writer.Write((ushort)size);
            writer.Write(new byte[]{0xF7,0,0});
            for(int i=0;i<256;i++) { Color color=i<palette.Count ? palette[i] : Color.Black; writer.Write(color.R);writer.Write(color.G);writer.Write(color.B); }
            writer.Write(new byte[]{0x21,0xFF,0x0B}); writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0")); writer.Write(new byte[]{3,1,0,0,0});
            for(int f=0;f<frames.Count;f++)
            {
                int delay=Math.Max(1,(int)Math.Round((f+1)*100/fps)-(int)Math.Round(f*100/fps));
                writer.Write(new byte[]{0x21,0xF9,4,4}); writer.Write((ushort)delay);writer.Write(new byte[]{0,0});
                writer.Write((byte)0x2C);writer.Write((ushort)0);writer.Write((ushort)0);writer.Write((ushort)size);writer.Write((ushort)size);writer.Write((byte)0);
                writer.Write((byte)8);
                byte[] compressed=Compress(Indexed(frames[f],palette,cache));
                for(int p=0;p<compressed.Length;p+=255) { int count=Math.Min(255,compressed.Length-p);writer.Write((byte)count);writer.Write(compressed,p,count); }
                writer.Write((byte)0);
            }
            writer.Write((byte)0x3B);
        }
        // Ask the independent platform decoder to read every encoded frame before reporting success.
        using(Image check=Image.FromFile(gifPath))
        using(var decoded=new Bitmap(size,size))
        using(Graphics graphics=Graphics.FromImage(decoded))
        {
            if(check.GetFrameCount(FrameDimension.Time)!=frames.Count) throw new InvalidOperationException("GIF frame count mismatch: "+gifPath);
            for(int frame=0;frame<frames.Count;frame++)
            {
                check.SelectActiveFrame(FrameDimension.Time,frame);
                graphics.DrawImageUnscaled(check,0,0);
            }
        }
        const int thumb=192,pad=8,caption=24;
        using(var sheet=new Bitmap(4*(thumb+pad)+pad,2*(thumb+caption+pad)+caption+pad))
        using(Graphics graphics=Graphics.FromImage(sheet))
        using(var font=new Font("Segoe UI",10))
        {
            graphics.Clear(Color.FromArgb(16,16,22));
            graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode=PixelOffsetMode.Half;
            graphics.DrawString(label,font,Brushes.White,pad,4);
            for(int i=0;i<8;i++)
            {
                int frame=(int)Math.Round(i*(sources.Length-1)/7.0);
                int x=pad+(i%4)*(thumb+pad),y=caption+pad+(i/4)*(thumb+caption+pad);
                using(Bitmap crop=Crop(sources[frame],size)) graphics.DrawImage(crop,new Rectangle(x,y,thumb,thumb));
                graphics.DrawString("frame "+frame.ToString("D3"),font,Brushes.LightGray,x,y+thumb+2);
            }
            sheet.Save(contactPath,ImageFormat.Png);
        }
        return palette.Count;
    }
}
'@

$captureRoot = [System.IO.Path]::GetFullPath($CaptureDirectory)
$manifestPath = Join-Path $captureRoot 'capture-manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Capture manifest not found: $manifestPath" }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $captureRoot 'Review' }
$reviewRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($reviewRoot) | Out-Null
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$items = @()
foreach ($clip in $manifest.clips) {
    if ($clip.path -notlike $Only) { continue }
    if ($clip.path -notmatch '^\d{2}/[a-z-]+$') { throw "Unexpected clip path: $($clip.path)" }
    $clipRoot = Join-Path $captureRoot $clip.path
    $sources = [string[]]@(for ($i = 0; $i -lt $clip.frames; $i++) {
        $framePath = Join-Path $clipRoot ('frame-{0:D3}.png' -f $i)
        if (-not (Test-Path -LiteralPath $framePath)) { throw "Frame not found: $framePath" }
        $framePath
    })
    $stem = $clip.path.Replace('/', '-')
    $gif = $stem + '.gif'
    $contact = $stem + '-contact.png'
    $colors = [CoinboundCombatReview]::Review($sources, (Join-Path $reviewRoot $gif), (Join-Path $reviewRoot $contact),
        ($clip.hero + ' - ' + $clip.path), $CropSize, ($manifest.framesPerSecond * $PlaybackSpeed))
    $items += [pscustomobject]@{ clip = $clip.path; hero = $clip.hero; gif = $gif; contact = $contact; frames = $clip.frames; colors = $colors }
    Write-Output ($clip.path + ': ' + $clip.frames + ' rendered frames -> ' + $gif)
}

$cards = foreach ($item in $items) {
    $label = [System.Net.WebUtility]::HtmlEncode($item.hero + ' - ' + $item.clip)
    '<article><h2>' + $label + '</h2><a href="' + $item.gif + '"><img src="' + $item.gif + '" alt="' + $label + '"></a><p><a href="' + $item.contact + '">Eight representative rendered frames</a></p></article>'
}
$stillCards = foreach ($file in (Get-ChildItem -LiteralPath $captureRoot -Filter '*.png' -File)) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $reviewRoot $file.Name) -Force
    $label = [System.Net.WebUtility]::HtmlEncode($file.BaseName)
    '<article><h2>' + $label + '</h2><a href="' + $file.Name + '"><img class="still" src="' + $file.Name + '" alt="' + $label + '"></a></article>'
}
$html = '<!doctype html><meta charset="utf-8"><title>Coinbound runtime animation review</title><style>body{background:#101016;color:#dbe0ef;font:15px Segoe UI,sans-serif;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(384px,1fr));gap:24px}article{background:#191c29;padding:12px}h2{font-size:17px;margin:0 0 12px}a{color:#9fd4e7}img{image-rendering:pixelated;max-width:100%}.still{width:100%}</style><h1>Actual rendered dungeon animations</h1><p>GIFs are crops of runtime PNGs. Original PNGs remain unchanged. Contact sheets show eight sampled frames; GIF palette quantization is only for preview encoding.</p><main>' + ($cards -join "`n") + ($stillCards -join "`n") + '</main>'
[System.IO.File]::WriteAllText((Join-Path $reviewRoot 'index.html'), $html, [System.Text.UTF8Encoding]::new($false))
$reviewManifest = [pscustomobject]@{ source = $manifestPath; cropSize = $CropSize; playbackSpeed = $PlaybackSpeed; clips = $items }
[System.IO.File]::WriteAllText((Join-Path $reviewRoot 'review-manifest.json'), ($reviewManifest | ConvertTo-Json -Depth 6), [System.Text.UTF8Encoding]::new($false))
Write-Output ('Review ready: ' + (Join-Path $reviewRoot 'index.html'))
