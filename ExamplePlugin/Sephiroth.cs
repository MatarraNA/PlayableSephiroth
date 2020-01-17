using System;
using System.Collections.Generic;
using System.Text;
using RoR2;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using RoR2.Projectile;
using BepInEx;
using R2API.Utils;
using System.Reflection;
using System.Linq;
using RoR2.UI;
using System.Collections.ObjectModel;
using System.Collections;
using System.IO;
using RoR2.Skills;
using R2API.AssetPlus;
using Sephiroth;
using KinematicCharacterController;
using MiniRpcLib;
using MiniRpcLib.Action;
using UnityEngine.Networking;
using UnityEngine.UI;
using static Sephiroth.Sephiroth;

namespace Sephiroth
{
    // dependencies
    [BepInDependency("com.bepis.r2api")]
    [BepInPlugin(MODUID, "Sephiroth", "1.0.0")]
    [R2APISubmoduleDependency(nameof(SurvivorAPI), nameof(SkillAPI), nameof(EntityAPI), nameof(EffectAPI), nameof(AssetPlus), nameof(PrefabAPI))]

    public class Sephiroth : BaseUnityPlugin
    {
        // mod vars
        public const string MODUID = "com.Matarra.Sephiroth";

        #region NETWORK
        // RPC
        public static IRpcAction<ServerDamageContainer> NetRequestDoDamage = null;
        public static IRpcAction<ServerBuffContainer> NetRequestGiveBuff = null;
        public static IRpcAction<ClientFxContainer> NetRequestSpawnFx = null;
        public static IRpcAction<ClientFxContainer> NetServerRequestSpawnFx = null;
        public static IRpcAction<ClientAnimatorBoolContainer> NetServerRequestAnimBool = null;
        public static IRpcAction<ClientAnimatorBoolContainer> NetClientRequestAnimBool = null;
        public static IRpcAction<ClientAnimatorFloatContainer> NetServerRequestAnimFloat = null;
        public static IRpcAction<ClientAnimatorFloatContainer> NetClientRequestAnimFloat = null;
        public static IRpcAction<ClientAnimatorTriggerContainer> NetServerRequestAnimTrigger = null;
        public static IRpcAction<ClientAnimatorTriggerContainer> NetClientRequestAnimTrigger = null;
        // RPC  RPC     RPC     RPC     RPC     RPC     RPC     RPC //  NetworkUser.readOnlyInstancesList
        [Server]
        private void RPCHandleServerDamage(NetworkUser arg1, ServerDamageContainer arg2)
        {
            // deal the DAMAGES
            try
            {
                arg2.DealDamage();
            }
            catch (Exception ex) { } // not very important if this fails
        }
        [Server]
        private void RPCHandleServerBuff(NetworkUser arg1, ServerBuffContainer arg2)
        {
            try
            {
                arg2.Execute();
            }
            catch (Exception ex) { } // not very important if this fails
        }
        [Server]
        private void RPCHandleServerToClientFx(NetworkUser arg1, ClientFxContainer arg2)
        {
            foreach (var user in NetworkUser.readOnlyInstancesList)
            {
                try
                {
                    NetRequestSpawnFx.Invoke(arg2, user);
                }
                catch (Exception ex) { } // not important if this fails
            }
        }
        [Client]
        private void RPCHandleClientFx(NetworkUser arg1, ClientFxContainer arg2)
        {
            try
            {
                StartCoroutine(RPCPlayFx(Assets.MainAssetBundle.LoadAsset<GameObject>(arg2.prefabName), arg2.position, arg2.rotation, arg2.emissionLife, arg2.destroyLife));
            }
            catch (Exception ex) { } // not very important if this fails
        }
        private IEnumerator RPCPlayFx(GameObject particle, Vector3 pos, Quaternion rot, float emissionLife, float destroyLife)
        {
            var go = GameObject.Instantiate(particle, pos, rot);
            var particles = go.GetComponentsInChildren<ParticleSystem>();
            Destroy(go, destroyLife);
            yield return new WaitForSeconds(emissionLife);
            foreach (var obj in particles)
            {
                if (obj != null)
                    obj.Stop();
            }
        }

        // ANIM RPC
        [Server]
        private void RPCHandleServerAnimBool(NetworkUser arg1, ClientAnimatorBoolContainer arg2)
        {
            try
            {
                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    if (arg1 != null)
                        if (arg1.netId == user.netId) continue; // skip issuer as it happens client side
                    NetClientRequestAnimBool.Invoke(arg2, user);
                }
            }
            catch (Exception ex) { }
        }
        [Client]
        private void RPCHandleClientAnimBool(NetworkUser arg1, ClientAnimatorBoolContainer arg2)
        {
            try
            {
                arg2.Execute();
            }
            catch (Exception ex) { }
        }
        [Server]
        private void RPCHandleServerAnimFloat(NetworkUser arg1, ClientAnimatorFloatContainer arg2)
        {
            try
            {
                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    if (arg1 != null)
                        if (arg1.netId == user.netId) continue; // skip issuer as it happens client side
                    NetClientRequestAnimFloat.Invoke(arg2, user);
                }
            }
            catch (Exception ex) { }
        }
        [Client]
        private void RPCHandleClientAnimFloat(NetworkUser arg1, ClientAnimatorFloatContainer arg2)
        {
            try
            {
                arg2.Execute();
            }
            catch (Exception ex) { }
        }
        [Server]
        private void RPCHandleServerAnimTrigger(NetworkUser arg1, ClientAnimatorTriggerContainer arg2)
        {
            try
            {
                foreach (var user in NetworkUser.readOnlyInstancesList)
                {
                    if (arg1 != null)
                    {
                        if (arg1.netId == user.netId) continue; // skip issuer as it happens client side
                    }
                    NetClientRequestAnimTrigger.Invoke(arg2, user);
                }
            }
            catch (Exception ex) { }
        }
        [Client]
        private void RPCHandleClientAnimTrigger(NetworkUser arg1, ClientAnimatorTriggerContainer arg2)
        {
            try
            {
                arg2.Execute();
            }
            catch (Exception ex) { }
        }

        public class ServerDamageContainer : MessageBase
        {
            public DamageInfo damage;
            public GameObject enemyGO;
            private HealthComponent _healthComponent;
            public ServerDamageContainer(DamageInfo damage, GameObject enemyHurboxGO)
            {
                this.damage = damage;
                this.enemyGO = enemyHurboxGO;
            }
            public void DealDamage()
            {
                _healthComponent = enemyGO.GetComponentInChildren<HealthComponent>();
                _healthComponent.TakeDamage(damage);
                GlobalEventManager.instance.OnHitEnemy(damage, _healthComponent.gameObject);
                GlobalEventManager.instance.OnHitAll(damage, _healthComponent.gameObject);
            }

            public override void Deserialize(NetworkReader reader)
            {
                damage = new DamageInfo();
                damage.attacker = reader.ReadGameObject();
                damage.crit = reader.ReadBoolean();
                damage.damage = reader.ReadSingle();
                damage.damageColorIndex = reader.ReadDamageColorIndex();
                damage.damageType = reader.ReadDamageType();
                damage.force = reader.ReadVector3();
                damage.procCoefficient = reader.ReadSingle();
                damage.position = reader.ReadVector3();

                // gameob
                enemyGO = reader.ReadGameObject();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(damage.attacker);
                writer.Write(damage.crit);
                writer.Write(damage.damage);
                writer.Write(damage.damageColorIndex);
                writer.Write(damage.damageType);
                writer.Write(damage.force);
                writer.Write(damage.procCoefficient);
                writer.Write(damage.position);

                // gameobject time
                writer.Write(enemyGO);
            }
        }
        public class ServerBuffContainer : MessageBase
        {
            public BuffIndex buff;
            public float buffDuration;
            public CharacterBody body;
            public ServerBuffContainer(BuffIndex buff, float buffDuration, CharacterBody body)
            {
                this.buff = buff;
                this.buffDuration = buffDuration;
                this.body = body;
            }
            public void Execute()
            {
                body.AddTimedBuff(buff, buffDuration);
            }

            public override void Deserialize(NetworkReader reader)
            {
                // buff
                buff = (BuffIndex)Enum.Parse(typeof(BuffIndex), reader.ReadString());

                buffDuration = reader.ReadSingle();
                body = reader.ReadGameObject().GetComponent<CharacterBody>();
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(buff.ToString());
                writer.Write(buffDuration);
                writer.Write(body.gameObject);
            }
        }
        public class ClientFxContainer : MessageBase
        {
            public string prefabName;
            public float emissionLife;
            public float destroyLife;
            public Vector3 position;
            public Quaternion rotation;

            public ClientFxContainer(string prefabName, float emissionLife, float destroyLife, Vector3 pos, Quaternion rot)
            {
                this.prefabName = prefabName;
                this.emissionLife = emissionLife;
                this.destroyLife = destroyLife;
                this.position = pos;
                this.rotation = rot;
            }

            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(prefabName);
                writer.Write(emissionLife);
                writer.Write(destroyLife);
                writer.Write(position);
                writer.Write(rotation);
            }
            public override void Deserialize(NetworkReader reader)
            {
                prefabName = reader.ReadString();
                emissionLife = reader.ReadSingle();
                destroyLife = reader.ReadSingle();
                position = reader.ReadVector3();
                rotation = reader.ReadQuaternion();
            }
        }
        public class ServerMagnetContainer : MessageBase
        {
            public float killTime;
            public float orbDuration;
            public Vector3 spawnPoint;
            public float maxDistanceFromOrb;
            public ServerMagnetContainer(float killTime, float orbDuration, Vector3 spawnPos, float maxDistFromOrb)
            {
                this.killTime = killTime;
                this.orbDuration = orbDuration;
                this.spawnPoint = spawnPos;
                this.maxDistanceFromOrb = maxDistFromOrb;
            }
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(killTime);
                writer.Write(orbDuration);
                writer.Write(spawnPoint);
                writer.Write(maxDistanceFromOrb);
            }
            public override void Deserialize(NetworkReader reader)
            {
                killTime = reader.ReadSingle();
                orbDuration = reader.ReadSingle();
                spawnPoint = reader.ReadVector3();
                maxDistanceFromOrb = reader.ReadSingle();
            }
        }
        public class ClientAnimatorBoolContainer : MessageBase
        {
            public GameObject characterDirection;
            public bool animBool;
            public string animName;

            public void Execute()
            {
                var animator = characterDirection.GetComponent<CharacterDirection>();
                animator.modelAnimator.SetBool(animName, animBool);
            }
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(characterDirection);
                writer.Write(animBool);
                writer.Write(animName);
            }
            public override void Deserialize(NetworkReader reader)
            {
                characterDirection = reader.ReadGameObject();
                animBool = reader.ReadBoolean();
                animName = reader.ReadString();
            }
        }
        public class ClientAnimatorFloatContainer : MessageBase
        {
            public GameObject characterDirection;
            public float animFloat;
            public string animName;

            public void Execute()
            {
                var animator = characterDirection.GetComponent<CharacterDirection>();
                animator.modelAnimator.SetFloat(animName, animFloat);
            }
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(characterDirection);
                writer.Write(animFloat);
                writer.Write(animName);
            }
            public override void Deserialize(NetworkReader reader)
            {
                characterDirection = reader.ReadGameObject();
                animFloat = reader.ReadSingle();
                animName = reader.ReadString();
            }
        }
        public class ClientAnimatorTriggerContainer : MessageBase
        {
            public GameObject characterDirection;
            public string animName;

            public void Execute()
            {
                var animator = characterDirection.GetComponent<CharacterDirection>();
                animator.modelAnimator.SetTrigger(animName);
            }
            public override void Serialize(NetworkWriter writer)
            {
                writer.Write(characterDirection);
                writer.Write(animName);
            }
            public override void Deserialize(NetworkReader reader)
            {
                characterDirection = reader.ReadGameObject();
                animName = reader.ReadString();
            }
        }
        #endregion

        // init
        private void Awake()
        {
            // first generate our assets
            Assets.PopulateAssets();

            // now register seph as a character
            RegisterSephiroth();

            // TEST TODO ERASE
            //On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };

            // register networking
            RegisterNetworking();
        }

        private void RegisterNetworking()
        {
            var rpc = MiniRpc.CreateInstance(MODUID);
            if (NetRequestDoDamage == null)
                NetRequestDoDamage = rpc.RegisterAction<ServerDamageContainer>(Target.Server, RPCHandleServerDamage);
            if (NetRequestGiveBuff == null)
                NetRequestGiveBuff = rpc.RegisterAction<ServerBuffContainer>(Target.Server, RPCHandleServerBuff);
            if (NetRequestSpawnFx == null)
                NetRequestSpawnFx = rpc.RegisterAction<ClientFxContainer>(Target.Client, RPCHandleClientFx);
            if (NetServerRequestSpawnFx == null)
                NetServerRequestSpawnFx = rpc.RegisterAction<ClientFxContainer>(Target.Server, RPCHandleServerToClientFx);
            if (NetServerRequestAnimBool == null)
                NetServerRequestAnimBool = rpc.RegisterAction<ClientAnimatorBoolContainer>(Target.Server, RPCHandleServerAnimBool);
            if (NetClientRequestAnimBool == null)
                NetClientRequestAnimBool = rpc.RegisterAction<ClientAnimatorBoolContainer>(Target.Client, RPCHandleClientAnimBool);
            if (NetServerRequestAnimFloat == null)
                NetServerRequestAnimFloat = rpc.RegisterAction<ClientAnimatorFloatContainer>(Target.Server, RPCHandleServerAnimFloat);
            if (NetClientRequestAnimFloat == null)
                NetClientRequestAnimFloat = rpc.RegisterAction<ClientAnimatorFloatContainer>(Target.Client, RPCHandleClientAnimFloat);
            if (NetServerRequestAnimTrigger == null)
                NetServerRequestAnimTrigger = rpc.RegisterAction<ClientAnimatorTriggerContainer>(Target.Server, RPCHandleServerAnimTrigger);
            if (NetClientRequestAnimTrigger == null)
                NetClientRequestAnimTrigger = rpc.RegisterAction<ClientAnimatorTriggerContainer>(Target.Client, RPCHandleClientAnimTrigger);
        }

        private void RegisterModelSwap(GameObject sephPrefab, GameObject sephDisplayPrefab)
        {
            // swap the display model first
            var skinnedMeshes = sephDisplayPrefab.GetComponentsInChildren<SkinnedMeshRenderer>();
            var meshRenderers = sephDisplayPrefab.GetComponentsInChildren<MeshRenderer>();
            // sephs model GO
            var sephirothDisplayGO = Assets.MainAssetBundle.LoadAsset<GameObject>("seph_obj").InstantiateClone("seph_display", false);
            bool swapped = false;
            foreach (var skinMesh in skinnedMeshes)
            {
                if (!swapped)
                {
                    sephirothDisplayGO.transform.parent = skinMesh.transform.parent;
                    sephirothDisplayGO.transform.position = Vector3.zero;
                    swapped = true;
                }
                skinMesh.gameObject.SetActive(false);
            }
            foreach (var mesh in meshRenderers)
            {
                mesh.gameObject.SetActive(false);
            }

            // give sephiroth his ingame model swap component
            sephPrefab.AddComponent<SephirothModelSwap>();
        }

        private void RegisterSephiroth()
        {
            // gather our prefabs
            var sephPrefab = Resources.Load<GameObject>("prefabs/characterbodies/commandobody").InstantiateClone("SephBody");
            var commandoPrefab = Resources.Load<GameObject>("prefabs/characterbodies/commandobody");
            var sephDisplayPrefab = Resources.Load<GameObject>("prefabs/characterdisplays/commandodisplay").InstantiateClone("SephDisplay", false);

            // swap the models
            RegisterModelSwap(sephPrefab, sephDisplayPrefab);

            // Register sephs Skills
            RegisterSkills(sephPrefab);

            // register in body catalog
            var sephBody = sephPrefab.GetComponentInChildren<CharacterBody>();
            BodyCatalog.getAdditionalEntries += (list) => list.Add(sephPrefab);
            sephBody.baseNameToken = "Sephiroth";

            // Register sephs stats
            RegisterStats(sephBody);

            // character needs pod?
            if (sephBody.preferredPodPrefab == null)
                sephBody.preferredPodPrefab = commandoPrefab.GetComponentInChildren<CharacterBody>().preferredPodPrefab;

            // register sephs genericcharactermain
            var stateMachine = sephBody.GetComponent<EntityStateMachine>();
            stateMachine.mainStateType = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Sephiroth.Sephiroth));

            // register icon
            sephBody.portraitIcon = Assets.SephIcon.texture;

            // register survivor info
            SurvivorDef item = new SurvivorDef
            {
                name = "SEPHIROTH_BODY",
                bodyPrefab = sephPrefab,
                descriptionToken = "Kingdom Hearts, is light...",
                displayPrefab = sephDisplayPrefab,
                primaryColor = new Color(0.0f, 0.0f, 0.0f),
                unlockableName = "Sephiroth",
                survivorIndex = SurvivorIndex.Count + 1
            };
            SurvivorAPI.AddSurvivor(item);
            On.RoR2.BodyCatalog.Init += orig =>
            {
                orig();
                var bodyIndex = BodyCatalog.FindBodyIndex("SephBody");
                BodyCatalog.GetBodyPrefab(bodyIndex).GetComponent<CharacterBody>().baseNameToken = "Sephiroth";
            };
        }

        private void RegisterSkills(GameObject sephPrefab)
        {
            // first get the locator
            var locator = sephPrefab.GetComponentInChildren<SkillLocator>();

            // passive?
            locator.passiveSkill.enabled = true;
            locator.passiveSkill.skillDescriptionToken = "Attacks from Sephiroths Masamune leech life on hit.";
            locator.passiveSkill.skillNameToken = "Masamune";
            locator.passiveSkill.icon = Assets.SephIcon;

            // create skill def for primary
            SkillDef primaryDef = SkillDef.CreateInstance<SkillDef>();
            primaryDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Sephiroth.Primary));
            var field2 = typeof(EntityStates.SerializableEntityStateType)?.GetField("_typeName", BindingFlags.NonPublic | BindingFlags.Instance);
            field2?.SetValue(primaryDef.activationState, typeof(EntityStates.Sephiroth.Primary)?.AssemblyQualifiedName);
            primaryDef.baseRechargeInterval = 1f;
            primaryDef.baseMaxStock = 1;
            primaryDef.rechargeStock = 1;
            primaryDef.skillName = "SephSwipe";
            primaryDef.skillNameToken = "Heartless Angel";
            primaryDef.skillDescriptionToken = "Sephiroth unleashes a barrage of deadly precise attacks at high speed.";
            primaryDef.activationStateMachineName = locator.secondary.skillFamily.variants[0].skillDef.activationStateMachineName;
            primaryDef.isBullets = false;
            primaryDef.beginSkillCooldownOnSkillEnd = true;
            primaryDef.interruptPriority = EntityStates.InterruptPriority.Any;
            primaryDef.isCombatSkill = true;
            primaryDef.noSprint = false;
            primaryDef.canceledFromSprinting = false;
            primaryDef.mustKeyPress = true;
            primaryDef.requiredStock = 1;
            primaryDef.stockToConsume = 0;
            primaryDef.icon = Assets.SephIcon;

            // skill family for primary
            var primaryFam = SkillFamily.CreateInstance<SkillFamily>();
            primaryFam.defaultVariantIndex = 0;
            SkillFamily.Variant[] variantsPrimary = new SkillFamily.Variant[1];
            variantsPrimary[0] = new SkillFamily.Variant() { skillDef = primaryDef, unlockableName = "", viewableNode = new ViewablesCatalog.Node("M1", false) };
            primaryFam.variants = variantsPrimary;
            ((ScriptableObject)primaryFam).name = "sephPrimary";

            //primary
            SkillAPI.AddSkill(typeof(EntityStates.Sephiroth.Primary));
            SkillAPI.AddSkillDef(primaryDef);
            SkillAPI.AddSkillFamily(primaryFam);

            // add skills to player
            locator.primary.SetFieldValue<SkillFamily>("_skillFamily", primaryFam);
        }

        private void RegisterStats(CharacterBody sephBody)
        {
            sephBody.baseJumpPower = 20f;
            sephBody.baseJumpCount = 2;
            sephBody.baseMaxHealth = 145;
            sephBody.levelMaxHealth = 45;
            sephBody.baseRegen = 4f;
            sephBody.levelRegen = 0.7f;
            sephBody.baseArmor = 18;
        }

        // play particle system
        public static void PlayFx(GameObject fx, Vector3 pos, Quaternion rotation, float duration)
        {
            var thisFx = Instantiate(fx, pos, rotation);
            Destroy(thisFx, duration);
        }

    }

    public class SephirothModelSwap : MonoBehaviour
    {
        CharacterDirection direction;
        CharacterBody self;
        CharacterMotor motor;

        void Start()
        {
            // swap the model
            var seph = Instantiate(Assets.MainAssetBundle.LoadAsset<GameObject>("seph_obj"));
            direction = this.transform.root.GetComponentInChildren<CharacterDirection>();
            self = this.transform.root.GetComponentInChildren<CharacterBody>();
            motor = this.transform.root.GetComponentInChildren<CharacterMotor>();

            // set this stuffs
            foreach (var thisItem in direction.modelAnimator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                thisItem.gameObject.SetActive(false);
            }
            foreach (var thisItem in direction.modelAnimator.GetComponentsInChildren<MeshRenderer>())
            {
                thisItem.gameObject.SetActive(false);
            }
            self.crosshairPrefab = Resources.Load<GameObject>("prefabs/crosshair/simpledotcrosshair");
            seph.transform.position = direction.modelAnimator.transform.position;
            seph.transform.rotation = direction.modelAnimator.transform.rotation;
            seph.transform.SetParent(direction.modelAnimator.transform);
            direction.modelAnimator = seph.GetComponentInChildren<Animator>();

            // DISABLE ITEM DISPLAYING
            var model = self.GetComponentInChildren<CharacterModel>();
            if (model != null)
                model.itemDisplayRuleSet = null;
        }
    }
}
namespace EntityStates.Sephiroth
{
    public class Sephiroth : GenericCharacterMain
    {
        // networking vars
        ClientAnimatorBoolContainer isMovingContainer = null;
        ClientAnimatorBoolContainer isGroundedContainer = null;
        ClientAnimatorFloatContainer moveSpeedScaleContainer = null;
        ClientAnimatorFloatContainer yVelocityContainer = null;
        NetworkIdentity thisInstance;
        NetworkUser thisUser;
        private float speedScale = 1f; // place this here for less garbage
        private float netMsgCooldown = 0.1f; // cooldown between net message sending
        private float netMsgTimer = 0f;

        public override void OnEnter()
        {
            base.OnEnter();

            // check if authority
            if (!base.isAuthority) return;
            #region networking
            if (isMovingContainer == null)
            {
                isMovingContainer = new ClientAnimatorBoolContainer();
                isMovingContainer.characterDirection = base.characterDirection.gameObject;
                isMovingContainer.animName = "isMoving";
            }
            if (isGroundedContainer == null)
            {
                isGroundedContainer = new ClientAnimatorBoolContainer();
                isGroundedContainer.characterDirection = base.characterDirection.gameObject;
                isGroundedContainer.animName = "isGrounded";
            }
            if (yVelocityContainer == null)
            {
                yVelocityContainer = new ClientAnimatorFloatContainer();
                yVelocityContainer.characterDirection = base.characterDirection.gameObject;
                yVelocityContainer.animName = "yVelocity";
            }
            if( moveSpeedScaleContainer == null )
            {
                moveSpeedScaleContainer = new ClientAnimatorFloatContainer();
                moveSpeedScaleContainer.characterDirection = base.characterDirection.gameObject;
                moveSpeedScaleContainer.animName = "moveSpeedScale";
            }
            thisInstance = base.GetComponent<NetworkIdentity>();
            thisUser = NetworkUser.readOnlyInstancesList.Where(x => x.netId == thisInstance.netId).FirstOrDefault();
            #endregion
        }

        public override void Update()
        {
            base.Update();

            // TEST TODO ERASE
            if( Input.GetMouseButtonDown(1) )
            {
                base.characterBody.inventory.GiveItem(ItemIndex.Hoof);
                base.characterBody.inventory.GiveItem(ItemIndex.Syringe);
            }
            #region networking animation
            // handle animation shit
            if (characterMotor.isGrounded && characterMotor.velocity.magnitude > 0.01f)
            {
                isMovingContainer.animBool = true;
                if( Time.time > netMsgTimer )
                    NetServerRequestAnimBool.Invoke(isMovingContainer, thisUser);
                characterDirection.modelAnimator.SetBool("isMoving", true);
            }
            else
            {
                isMovingContainer.animBool = false;
                if( Time.time > netMsgTimer )
                    NetServerRequestAnimBool.Invoke(isMovingContainer, thisUser);
                characterDirection.modelAnimator.SetBool("isMoving", false);
            }
            // handle velocity net / client
            characterDirection.modelAnimator.SetFloat("yVelocity", characterMotor.velocity.y);
            yVelocityContainer.animFloat = characterMotor.velocity.y;
            if( Time.time > netMsgTimer )
                NetServerRequestAnimFloat.Invoke(yVelocityContainer, thisUser);

            // handle grounded net / client
            characterDirection.modelAnimator.SetBool("isGrounded", characterMotor.isGrounded);
            isGroundedContainer.animBool = characterMotor.isGrounded;
            if( Time.time > netMsgTimer )
                NetServerRequestAnimBool.Invoke(isGroundedContainer, thisUser);

            // handle scaling movement speed
            speedScale = base.characterBody.moveSpeed / base.characterBody.baseMoveSpeed;
            speedScale = (speedScale - 1f) * 0.5f + 1f;
            characterDirection.modelAnimator.SetFloat("moveSpeedScale", speedScale);
            moveSpeedScaleContainer.animFloat = speedScale;
            if( Time.time > netMsgTimer )
                NetServerRequestAnimFloat.Invoke(moveSpeedScaleContainer, thisUser);

            // HANDLE TIMER RESET
            if (Time.time > netMsgTimer)
                netMsgTimer = Time.time + netMsgCooldown;
            netMsgTimer += Time.deltaTime;
            #endregion
        }
        public override void OnExit()
        {
            base.OnExit();
        }
    }

    public class Primary : BaseState
    {

        private ClientAnimatorTriggerContainer netTriggerContainer; // to send animation data to other players
        private ClientAnimatorFloatContainer netFloatContainer; // to send attack speed "speed" data to other players
        private NetworkIdentity thisInstance;
        private NetworkUser thisUser;
        private SephirothModelSwap thisSephiroth; // reference this to call and cancel coroutines
        // animation
        private float animationSpeedMulti = 2f; // time mutliplier attack is played at
        private float maxAttackSpeed = 2f;

        // vars
        private float primaryGroundedForwardOffset = 2f;
        private float primaryGroundedRadius = 4.5f;
        private float primaryGroundedMaxYDiff = 3f;
        private float primaryGroundedDamage = 3f;
        private float primaryGroundedCoef = 0.75f;

        public override void OnEnter()
        {
            base.OnEnter();
            if (base.isAuthority)
            {
                // prepare anim
                netTriggerContainer = new ClientAnimatorTriggerContainer();
                netTriggerContainer.characterDirection = base.characterDirection.gameObject;
                // prepare network user
                thisInstance = base.GetComponent<NetworkIdentity>();
                thisUser = NetworkUser.readOnlyInstancesList.Where(x => x.netId == thisInstance.netId).FirstOrDefault();

                // get animation time
                hitDuration = GetAnimationTime("seph_ground_combo");
                // play anim
                base.characterDirection.modelAnimator.SetTrigger("isGroundCombo1");
                netTriggerContainer.animName = "isGroundCombo1";
                NetServerRequestAnimTrigger.Invoke(netTriggerContainer, thisUser);

                // force aim
                base.StartAimMode(hitDuration);

                // damage coroutine
                thisSephiroth = base.gameObject.GetComponentInChildren<SephirothModelSwap>();
                thisSephiroth.StartCoroutine(PrimaryGroundCoroutine());
            }
        }

        private IEnumerator PrimaryGroundCoroutine()
        {
            // play first hit sound
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_VOICE_1, base.characterBody.gameObject);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before first hitbox
            yield return new WaitForSeconds(hitDuration * 0.16f);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_VOICE_2, base.characterBody.gameObject);
            var enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if( ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef) )
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            
            // wait time before second hitbox
            yield return new WaitForSeconds(hitDuration * 0.12f);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before thirdhitbox
            yield return new WaitForSeconds(hitDuration * 0.08f);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            //AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before fourth hitbox
            yield return new WaitForSeconds(hitDuration * 0.11f);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_VOICE_1, base.characterBody.gameObject);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before fifth hitbox
            yield return new WaitForSeconds(hitDuration * 0.14f);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            //AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before sixed hitbox
            yield return new WaitForSeconds(hitDuration * 0.1f);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_VOICE_2, base.characterBody.gameObject);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);
            AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);

            // wait time before seventh hitbox
            yield return new WaitForSeconds(hitDuration * 0.14f);
            //AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_WIFF_SFX, base.characterBody.gameObject);
            enemies = CollectEnemies(primaryGroundedRadius, base.transform.position + base.characterDirection.forward * primaryGroundedForwardOffset, primaryGroundedMaxYDiff);
            if (ColliderDamage(enemies, primaryGroundedDamage, primaryGroundedCoef))
                AkSoundEngine.PostEvent(Sounds.LIGHT_ATTACK_HIT_SFX, base.characterBody.gameObject);

            yield break;
        }

        private Collider[] CollectEnemies(float radius, Vector3 position, float maxYDiff )
        {
            Collider[] array = Physics.OverlapSphere(position, radius, LayerIndex.entityPrecise.mask);
            array = array.Where(x => Mathf.Abs(x.ClosestPoint(base.transform.position).y - base.transform.position.y) <= maxYDiff).ToArray();
            return array;
        }
        private bool ColliderDamage(Collider[] array, float damageMulti, float coeff )
        {
            // now that we have our enemies, only get the ones within the Y dimension
            var hurtboxes = array.Where(x => x.GetComponent<HurtBox>() != null);
            List<HurtBoxGroup> allReadyDamaged = new List<HurtBoxGroup>();
            HurtBox hitSoundObj = null;
            foreach (var hurtBox in hurtboxes)
            {
                var hurtBox2 = hurtBox.GetComponentInChildren<HurtBox>();
                if (hurtBox2 == null) continue;
                if (hurtBox2.healthComponent == base.healthComponent) continue;
                if (allReadyDamaged.Where(x => x == hurtBox2.hurtBoxGroup).Count() > 0) continue; // already hit them
                if (hurtBox2.teamIndex == base.teamComponent.teamIndex) continue; // dont hit teammates LUL
                hitSoundObj = hurtBox2;
                allReadyDamaged.Add(hurtBox2.hurtBoxGroup);
                DamageInfo damageInfo = new DamageInfo();
                damageInfo.damage = damageStat * damageMulti;
                damageInfo.attacker = base.gameObject;
                damageInfo.procCoefficient = coeff;
                damageInfo.position = hurtBox2.transform.position;
                damageInfo.crit = base.RollCrit();

                // WEB HANDLE
                ServerDamageContainer container = new ServerDamageContainer(damageInfo, hurtBox2.healthComponent.gameObject);
                NetRequestDoDamage.Invoke(container);

                // play hit fx!
                Vector3 hitPos = hurtBox2.collider.ClosestPoint(base.transform.position);
                PlayFx(Assets.SephHitFx, hitPos, Quaternion.identity, 1f);
            }
            if (allReadyDamaged == null) return false;
            return allReadyDamaged.Count() > 0;
        }

        public override void OnExit()
        {
            base.OnExit();
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            if( base.isAuthority )
            {
                // move in direction of hit
                if( ForceMove() )
                {
                    // movement went off
                }
            }

            if (base.fixedAge >= this.hitDuration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }
       
        private Vector3 moveDir;
        private float maxForceMoveDist = 25f;
        private float minDistance = 5.0f;
        private float forceSpeedMulti = 3f; // gets calc'd from distance instead
        private Transform lockOnEnemy = null;
        private bool ForceMove()
        {
            RaycastHit hit;
            if (Physics.Raycast(base.transform.position, base.inputBank.aimDirection, out hit, maxForceMoveDist, LayerIndex.entityPrecise.mask))
            {
                // bingo we have a winner, check if unit is dead
                var hurtbox = hit.collider.transform.root.GetComponentInChildren<HurtBox>();
                    
                if( hurtbox != null)
                {
                    var healthComp = hurtbox.healthComponent;
                    if (healthComp == null) return false;
                    Vector3 hitPos = new Vector3(hit.point.x, base.transform.position.y, hit.point.z);
                    if( healthComp.alive && Vector3.Distance(hitPos, base.transform.position) >= minDistance )
                    {
                        if( healthComp.body != null )
                        {
                            if( !healthComp.body.isFlying )
                            {
                                lockOnEnemy = hit.collider.transform;
                            }
                        }
                    }
                }
            }
            if( lockOnEnemy != null )
            {
                
                moveDir = (lockOnEnemy.transform.position - base.transform.position).normalized;
                forceSpeedMulti = Vector3.Distance(lockOnEnemy.transform.position, base.transform.position);
                return true;
            }

            // failed to find suitable enemy
            return false;
        }

        private float GetAnimationTime(string clipName, float customSpeedScale = 1f)
        {
            if (customSpeedScale != 1)
                animationSpeedMulti = customSpeedScale;
            float attackSpeed = base.characterBody.attackSpeed;
            float scaledAttackSpeed = ((attackSpeed - 1f) * 0.5f) + 1f;
            scaledAttackSpeed = Mathf.Min(scaledAttackSpeed, maxAttackSpeed);
            base.characterDirection.modelAnimator.SetFloat("speedScale", scaledAttackSpeed);
            netFloatContainer = new ClientAnimatorFloatContainer();
            netFloatContainer.animFloat = scaledAttackSpeed;
            netFloatContainer.animName = "speedScale";
            NetServerRequestAnimFloat.Invoke(netFloatContainer, thisUser);
            float unScaledDuration = base.characterDirection.modelAnimator.runtimeAnimatorController.animationClips.Where(x => x.name.ToLower().Equals(clipName.ToLower())).First().length;
            return unScaledDuration / animationSpeedMulti / scaledAttackSpeed;
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Death;
        }

        private float hitDuration;
    }
}