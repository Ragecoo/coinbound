from pathlib import Path
import json,shutil
root=Path(__file__).resolve().parents[1]
src=root/'ура/map'; dst=root/'Assets/Coinbound/Resources/EnvironmentProps52V5'
dst.mkdir(parents=True,exist_ok=True)
manifest=json.loads((src/'manifest.json').read_text(encoding='utf-8-sig'))
assets=[]
for a in manifest['assets']:
 d=a['destruction']; pl=a['placement']
 item=dict(id=a['id'],name=a['name'],file=Path(a['file']).stem,width=a['width'],height=a['height'],
  pivot=a['pivot_px_bottom_left'],bounds=a['visual_bounds_relative_pivot_px'],solid=a['collision']['enabled'],
  boxes=a['collision']['rects_px_bottom_left'],hitboxes=a['damage_hitboxes_px_bottom_left'],
  hp=d.get('hp_base_damage_multiplier',0)*2,material=d.get('material',''),extra=d.get('extra_particles') or '',
  themes=a['themes'],walls=pl['allowed_single_walls'],corner=a.get('corner_direction') or '',
  contacts=[pl['contact_edges_px_bottom_left'][k] for k in ('N','E','S','W')],
  tall=a['tall_overhang'],ground=a['render_layer']=='ground',debris=[],survivors=[])
 for b in d.get('debris',[]): item['debris'].append(dict(file=b['asset'],offset=b['offset_px']))
 for b in d.get('survivors',[]): item['survivors'].append(dict(id=b['asset_id'],offset=b['offset_px'],solid=b['keep_collision']))
 # Authored flame points are derived from the unchanged base candle coordinates and exact compositions.
 bases={11:[(16,24),(10,19),(23,18)],18:[(8,14),(24,14)]}
 points=[]
 if a['id'] in bases: points=bases[a['id']]
 for c in a.get('composition',{}).get('components',[]):
  for x,y in bases.get(c['source_id'],[]):
   ox,oy=c['offset_px_top_left'];points.append((x+ox,y+a['height']-oy-32))
 if a['id']==47:points=[(16,38)]
 item['flames']=[dict(x=x-a['pivot_px_bottom_left'][0],y=y-a['pivot_px_bottom_left'][1]) for x,y in points]
 assets.append(item)
 (dst/'sprites').mkdir(exist_ok=True);shutil.copyfile(src/a['file'],dst/'sprites'/Path(a['file']).name)
(dst/'debris').mkdir(exist_ok=True)
for p in (src/'debris').glob('*.png'):shutil.copyfile(p,dst/'debris'/p.name)
(dst/'catalog.json').write_text(json.dumps(dict(assets=assets),ensure_ascii=False),encoding='utf-8')
shutil.copyfile(src/'docs/03_PLACEMENT_RECIPES.json',dst/'recipes.json')
print('Imported',len(assets),'unmodified sprites and 4 debris textures.')
