using System.Linq;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// The camera rigs use floor-level tracking: the rig's position is where the real floor is, and the
    /// headset adds the player's real height on top. So the rig must sit exactly on the ground.
    /// </summary>
    public static class PlayerRigUtility
    {
        public const string RigName = "[BuildingBlock] Camera Rig";

        public static GameObject FindRig() => GameObject.Find(RigName);

        /// <summary>Height of the walkable ground under the rig, or null if there is none.</summary>
        public static float? GroundBelow(GameObject rig)
        {
            var p = rig.transform.position;
            var terrain = Terrain.activeTerrains.FirstOrDefault(t =>
            {
                var tp = t.transform.position; var s = t.terrainData.size;
                return p.x >= tp.x && p.x <= tp.x + s.x && p.z >= tp.z && p.z <= tp.z + s.z;
            });
            if (terrain != null) return terrain.SampleHeight(p) + terrain.transform.position.y;

            // Otherwise the lowest solid surface straight below (the floor, not a table or the rig itself).
            Physics.SyncTransforms();
            var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 50f, p.z), Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.collider.transform.IsChildOf(rig.transform))
                .OrderBy(h => h.point.y).ToList();
            if (hits.Count == 0) return null;
            // A room's floor may sit a little above an outdoor ground plane: take the top of that lowest layer.
            float lowest = hits[0].point.y;
            return hits.Where(h => h.point.y < lowest + 0.3f).Max(h => h.point.y);
        }
    }
}
