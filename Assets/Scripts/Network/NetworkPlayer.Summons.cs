using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PetGame.Network
{
    public struct SummonSnapshot
    {
        public uint id;
        public string prefabName;
        public string factionTag;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool flipX;
        public float health;
        public float maxHealth;
        public int animationHash;
        public float animationTime;
        public float animationSpeed;
        public EnemyAnimatorParameter[] animationParameters;
    }

    public partial class NetworkPlayer
    {
        private sealed class LocalSummon
        {
            public CharacterEntity entity;
            public string prefabName;
            public string factionTag;
        }

        private readonly Dictionary<uint, LocalSummon> localSummons = new Dictionary<uint, LocalSummon>();
        private readonly Dictionary<uint, NetworkSummonReplica> summonReplicas = new Dictionary<uint, NetworkSummonReplica>();
        private readonly Dictionary<string, GameObject> summonPrefabs = new Dictionary<string, GameObject>();
        private uint nextSummonId = 1;
        private float summonSyncTimer;

        public void RegisterLocalSummon(GameObject obj, string prefabName, string factionTag)
        {
            if (!isOwned || obj == null) return;
            var entity = obj.GetComponent<CharacterEntity>();
            if (entity == null || obj.GetComponent<SummonedEntityTracker>() == null) return;
            localSummons[nextSummonId++] = new LocalSummon
            {
                entity = entity, prefabName = prefabName, factionTag = factionTag
            };
            summonSyncTimer = 0f;
        }

        private void UpdateSummonSync()
        {
            if (!isOwned || !NetworkClient.ready) return;
            summonSyncTimer -= Time.deltaTime;
            if (summonSyncTimer > 0f) return;
            summonSyncTimer = 0.1f;
            var snapshots = new List<SummonSnapshot>();
            foreach (uint id in new List<uint>(localSummons.Keys))
            {
                LocalSummon summon = localSummons[id];
                CharacterEntity entity = summon.entity;
                if (entity == null || !entity.gameObject.activeInHierarchy ||
                    entity.RuntimeStats == null || !entity.RuntimeStats.IsAlive)
                {
                    localSummons.Remove(id);
                    continue;
                }
                var animator = entity.GetComponent<Animator>();
                bool animated = animator != null && animator.runtimeAnimatorController != null;
                AnimatorStateInfo state = animated
                    ? (animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0)) : default;
                var sprite = entity.GetComponent<SpriteRenderer>();
                snapshots.Add(new SummonSnapshot
                {
                    id = id, prefabName = summon.prefabName, factionTag = summon.factionTag,
                    position = entity.transform.position, rotation = entity.transform.rotation,
                    scale = entity.transform.localScale, flipX = sprite != null && sprite.flipX,
                    health = entity.RuntimeStats.currentHealth, maxHealth = entity.RuntimeStats.maxHealth,
                    animationHash = state.fullPathHash, animationTime = state.normalizedTime,
                    animationSpeed = animated ? animator.speed : 1f,
                    animationParameters = NetworkEnemySpawner.CaptureAnimationParameters(animator)
                });
            }
            // Full snapshots also remove expired waves and populate clients that join mid-summon.
            CmdSyncSummons(SceneManager.GetActiveScene().path, snapshots.ToArray());
        }

        [Command]
        private void CmdSyncSummons(string scene, SummonSnapshot[] snapshots)
        {
            RpcSyncSummons(scene, snapshots);
        }

        [ClientRpc]
        private void RpcSyncSummons(string scene, SummonSnapshot[] snapshots)
        {
            if (isOwned || scene != SceneManager.GetActiveScene().path || snapshots == null) return;
            var alive = new HashSet<uint>();
            foreach (SummonSnapshot snapshot in snapshots)
            {
                alive.Add(snapshot.id);
                if (!summonReplicas.TryGetValue(snapshot.id, out NetworkSummonReplica replica) || replica == null)
                {
                    GameObject prefab = FindSummonPrefab(snapshot.prefabName);
                    if (prefab == null) continue;
                    GameObject obj = Instantiate(prefab, snapshot.position, snapshot.rotation);
                    obj.name = $"RemoteSummon_{netId}_{snapshot.id}";
                    obj.tag = snapshot.factionTag;
                    replica = obj.AddComponent<NetworkSummonReplica>();
                    replica.Initialize(this, snapshot.id);
                    obj.SetActive(true);
                    summonReplicas[snapshot.id] = replica;
                }
                replica.ApplySnapshot(snapshot);
            }
            foreach (uint id in new List<uint>(summonReplicas.Keys))
            {
                if (alive.Contains(id)) continue;
                if (summonReplicas[id] != null) Destroy(summonReplicas[id].gameObject);
                summonReplicas.Remove(id);
            }
        }

        private GameObject FindSummonPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return null;
            if (summonPrefabs.TryGetValue(prefabName, out GameObject prefab)) return prefab;
            // Skill references also support summon prefabs outside the character Resources folder.
            foreach (SummonSkillEffectData effect in Resources.FindObjectsOfTypeAll<SummonSkillEffectData>())
            {
                if (effect.summonPrefab != null && effect.summonPrefab.name == prefabName)
                {
                    summonPrefabs[prefabName] = effect.summonPrefab;
                    return effect.summonPrefab;
                }
            }
            // LoadAll searches nested character folders, including JiZhenPrefab.
            foreach (GameObject candidate in Resources.LoadAll<GameObject>("Prefabs/Entity/Characters"))
                summonPrefabs[candidate.name] = candidate;
            if (summonPrefabs.TryGetValue(prefabName, out prefab)) return prefab;
            Debug.LogWarning($"[NetworkPlayer] Cannot find summon prefab: {prefabName}");
            return null;
        }

        private void CleanupSummonReplicas()
        {
            foreach (NetworkSummonReplica replica in summonReplicas.Values)
                if (replica != null) Destroy(replica.gameObject);
            summonReplicas.Clear();
        }

        public void RequestDamageSummon(uint ownerNetId, uint summonId, float damage)
        {
            if (isOwned) CmdDamageSummon(ownerNetId, summonId, damage);
        }

        [Command]
        private void CmdDamageSummon(uint ownerNetId, uint summonId, float damage)
        {
            if (!NetworkServer.spawned.TryGetValue(ownerNetId, out NetworkIdentity identity)) return;
            var owner = identity.GetComponent<NetworkPlayer>();
            if (owner != null && owner.connectionToClient != null)
                owner.TargetDamageSummon(summonId, damage);
        }

        [TargetRpc]
        private void TargetDamageSummon(uint summonId, float damage)
        {
            if (localSummons.TryGetValue(summonId, out LocalSummon summon) && summon.entity != null)
                summon.entity.TakeDamage(damage);
        }
    }
}
