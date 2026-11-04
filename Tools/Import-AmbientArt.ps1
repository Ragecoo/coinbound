param([Parameter(Mandatory=$true)][string]$Source)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskSourceDir = Join-Path $taskRoot 'ArtSource/Atmosphere'
$taskOutputDir = Join-Path $taskRoot 'Assets/Coinbound/Resources/AmbientArt'
New-Item -ItemType Directory -Force $taskSourceDir, $taskOutputDir | Out-Null
Copy-Item -LiteralPath $Source -Destination (Join-Path $taskSourceDir 'generated-decor.png')
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class AmbientImport {
    public static void Run(string path, string output, string preview) {
        using(var source=new Bitmap(path))
        using(var result=new Bitmap(240,192,PixelFormat.Format32bppArgb)) {
            int[] columns={0,280,560,840,1120,1402}, rows={0,305,612,850,1122};
            for(int i=0;i<20;i++) {
                int col=i%5,row=i/5;
                int minX=columns[col+1],minY=rows[row+1],maxX=columns[col],maxY=rows[row];
                for(int y=rows[row];y<rows[row+1];y++) for(int x=columns[col];x<columns[col+1];x++) {
                    if(source.GetPixel(x,y).A<128)continue;
                    minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
                }
                int maxWidth=i==15?18:40, maxHeight=i==15?12:38;
                double scale=Math.Min((double)maxWidth/(maxX-minX+1),(double)maxHeight/(maxY-minY+1));
                int width=Math.Max(1,(int)Math.Round((maxX-minX+1)*scale));
                int height=Math.Max(1,(int)Math.Round((maxY-minY+1)*scale));
                int left=col*48+(48-width)/2, top=row*48+40-height;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++) {
                    int sx=Math.Min(maxX,minX+(int)((x+.5)*(maxX-minX+1)/width));
                    int sy=Math.Min(maxY,minY+(int)((y+.5)*(maxY-minY+1)/height));
                    Color c=source.GetPixel(sx,sy);
                    if(c.A<128)continue;
                    result.SetPixel(left+x,top+y,Color.FromArgb(255,Q(c.R),Q(c.G),Q(c.B)));
                }
            }
            result.Save(output,ImageFormat.Png);
            using(var sheet=new Bitmap(960,768)) {
                for(int y=0;y<768;y++)for(int x=0;x<960;x++) {
                    Color c=result.GetPixel(x/4,y/4);
                    sheet.SetPixel(x,y,c.A==0?Color.FromArgb(26,27,39):c);
                }
                sheet.Save(preview,ImageFormat.Png);
            }
        }
    }
    static int Q(byte b){return Math.Min(255,(int)Math.Round(b/8.0)*8);}
}
'@
[AmbientImport]::Run($Source,(Join-Path $taskOutputDir 'decor.png'),(Join-Path $taskSourceDir 'native-preview.png'))
