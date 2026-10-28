/** Mask bits denote adjacent FLOOR, with screen Y increasing downwards. */
export const FLOOR_BITS = Object.freeze({N:1,E:2,S:4,W:8,NE:16,SE:32,SW:64,NW:128});

/** Remove diagonal bevels already represented by an exposed cardinal edge. */
export function normalizeWallMask(raw) {
  const cardinals = raw & 15;
  let mask = cardinals;
  if ((raw & 16) && !(cardinals & 3)) mask |= 16;
  if ((raw & 32) && !(cardinals & 6)) mask |= 32;
  if ((raw & 64) && !(cardinals & 12)) mask |= 64;
  if ((raw & 128) && !(cardinals & 9)) mask |= 128;
  return mask;
}

export function wallSpriteFromMask(raw) {
  return `wall.${normalizeWallMask(raw).toString(16).toUpperCase().padStart(2,'0')}`;
}

/**
 * Call for a WALL cell. isFloor(x,y) must return false outside the map.
 * Floors, stairs and fountain cells count as floor for this lookup.
 */
export function wallSpriteAt(x,y,isFloor) {
  const neighbors = [[0,-1,1],[1,0,2],[0,1,4],[-1,0,8],
    [1,-1,16],[1,1,32],[-1,1,64],[-1,-1,128]];
  let raw=0;
  for (const [dx,dy,bit] of neighbors) if(isFloor(x+dx,y+dy))raw|=bit;
  return wallSpriteFromMask(raw);
}
