#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using AnimatorAsCode.V1.VRC;
using nadena.dev.ndmf;
using net.rs64.PPRS;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(PPRSAnimatorPlugin))]
namespace net.rs64.PPRS
{
    public class PPRSAnimator : MonoBehaviour, IEditorOnly
    {
        [Header("PuniPuniRendererSettings")]
        public Renderer PPRSRenderer;
        public float NeutralOffset = 0.0f;
        public float LipAOffset = 0.5f;
        public float BlinkOffset = 0.25f;
        public float FlyOffset = 0.75f;

        [Header("Fascial")]
        public float BlinkLoopKeyTime = 180;
        public float BlinkingKeyTime = 10;
        public float LipSyncThreshold = 0.1f;

        [Header("FlyMovement")]
        public float MovementThreshold = 0.1f;
        public float MovingTranslationKeyTime = 10f;
        public float MovementPuniPuniScale = 0.5f;

        [Header("ScalingTransforms")]
        public Transform SyagamuTransform;
        public Transform NobiruTubureruTransform;
        public Transform MoveAnimationTransform;

        [Header("VRComponents")]
        public VRCPhysBone NobiruTubureruPhysBone;
        public VRCRotationConstraint GroundedPuniPuniConstraint;
        public VRCRotationConstraint FlyingPuniPuniConstraint;

    }

    public class PPRSAnimatorPlugin : Plugin<PPRSAnimatorPlugin>
    {
        private const string Offset_Property = "material._MainTex_ST";
        private const string Constraint_GlobalWeight = "GlobalWeight";
        private const string STR_Postfix = "_Stretch";
        private const string SQU_Postfix = "_Squish";
        private const bool UseWriteDefaults = false;
        protected override void Configure() { InPhase(BuildPhase.Generating).Run($"Generate {DisplayName}", Generate); }

        private void Generate(BuildContext ctx)
        {
            var pprsAnimator = ctx.AvatarRootObject.GetComponent<PPRSAnimator>();
            if (pprsAnimator == null) { return; }

            var aac = AacV1.Create(new AacConfiguration()
            {
                SystemName = nameof(PPRSAnimator),
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                AssetContainerProvider = new NDMFContainerProvider(ctx),
                DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults)
            });

            var ctrl = aac.NewAnimatorController();

            GenPPRS(pprsAnimator, ctrl, aac, ctx);

            var ma = MaAc.Create(new GameObject(nameof(PPRSAnimator)) { transform = { parent = ctx.AvatarRootTransform } });
            ma.NewMergeAnimator(ctrl, VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType.FX);
        }

        void GenPPRS(PPRSAnimator pprs, AacFlController ctrl, AacFlBase aac, BuildContext ctx)
        {
            {
                var SSlayer = ctrl.NewLayer("PPRS-SS-Layer").WithWeight(1.0f);

                var cStrSTRParm = SSlayer.FloatParameter(pprs.NobiruTubureruPhysBone.parameter + STR_Postfix);
                var cStrSQUParm = SSlayer.FloatParameter(pprs.NobiruTubureruPhysBone.parameter + SQU_Postfix);

                var tubureruAnimation = aac.NewClip().Animating(clip =>
                {
                    var tf = pprs.NobiruTubureruTransform;
                    clip.Animates(tf, "m_LocalScale.x").WithOneFrame(1.0f);
                    clip.Animates(tf, "m_LocalScale.z").WithOneFrame(1.0f);
                    clip.Animates(tf, "m_LocalScale.y").WithFrameCountUnit(keyFrames =>
                    {
                        keyFrames.Linear(0f, 1f).Linear(100f, 0.001f);
                    });
                });
                var nobiruAnimation = aac.NewClip().Animating(clip =>
                {
                    clip.Animates(pprs.NobiruTubureruTransform, "m_LocalScale.x").WithOneFrame(1.0f);
                    clip.Animates(pprs.NobiruTubureruTransform, "m_LocalScale.z").WithOneFrame(1.0f);
                    clip.Animates(pprs.NobiruTubureruTransform, "m_LocalScale.y").WithFrameCountUnit(keyFrames =>
                    {
                        keyFrames.Linear(0f, 1f).Linear(100f, 1f + pprs.NobiruTubureruPhysBone.maxStretch);
                    });
                }
                );

                var tubureruState = SSlayer.NewState("つぶれる").WithAnimation(tubureruAnimation).WithMotionTime(cStrSQUParm);
                var nobiruState = SSlayer.NewState("のびる").WithAnimation(nobiruAnimation).WithMotionTime(cStrSTRParm);

                SSlayer.WithDefaultState(nobiruState);

                nobiruState.TransitionsTo(tubureruState).When(cStrSQUParm.IsGreaterThan(0.0001f));
                tubureruState.TransitionsTo(nobiruState).When(cStrSQUParm.IsLessThan(0.0001f));
            }
            {
                var syagamuLayer = ctrl.NewLayer("PPRS-Syagamu-Layer").WithWeight(1.0f);
                var av3a = syagamuLayer.Av3();

                var syagamuState = syagamuLayer.NewState("しゃがむ").WithAnimation(aac.NewClip().Animating(clip =>
                {
                    var tf = pprs.SyagamuTransform;
                    clip.Animates(tf, "m_LocalScale.x").WithOneFrame(1.0f);
                    clip.Animates(tf, "m_LocalScale.z").WithOneFrame(1.0f);
                    clip.Animates(tf, "m_LocalScale.y").WithFrameCountUnit(keyFrames =>
                    {
                        keyFrames.Linear(0f, 0.001f).Linear(100f, 1.0f);
                    });
                })).WithMotionTime(av3a.Upright);

                syagamuLayer.WithDefaultState(syagamuState);
            }

            var layer = ctrl.NewLayer("PPRS-Layer").WithWeight(1.0f);

            var entryNullState = layer.NewState("Entry");
            layer.WithDefaultState(entryNullState);
            var av3 = layer.Av3();

            var moveCondition = av3.VelocityMagnitude.IsGreaterThan(pprs.MovementThreshold);
            var notMoveCondition = av3.VelocityMagnitude.IsLessThan(pprs.MovementThreshold);
            var grounded = av3.Grounded.IsTrue();
            var notGround = av3.Grounded.IsFalse();

            Action<AacFlEditClip> offsetInit = clip =>
            {
                clip.Animates(pprs.PPRSRenderer, Offset_Property + ".x").WithOneFrame(0.25f);
                clip.Animates(pprs.PPRSRenderer, Offset_Property + ".y").WithOneFrame(1.0f);
                clip.Animates(pprs.PPRSRenderer, Offset_Property + ".w").WithOneFrame(0.0f);
            };
            Func<AacFlEditClip, AacFlSettingCurve> getOffsetZ = clip =>
            {
                return clip.Animates(pprs.PPRSRenderer, Offset_Property + ".z");
            };
            Action<AacFlEditClip> moveScaleInit = clip =>
            {
                clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.x").WithOneFrame(1.0f);
                clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.y").WithOneFrame(1.0f);
                clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.z").WithOneFrame(1.0f);
            };
            Action<AacFlEditClip> constraintSwapGrounded = clip =>
            {
                clip.Animates(pprs.GroundedPuniPuniConstraint, Constraint_GlobalWeight).WithOneFrame(1.0f);
                clip.Animates(pprs.FlyingPuniPuniConstraint, Constraint_GlobalWeight).WithOneFrame(0.0f);
            };

            var idleSubStateMachine = layer.NewSubStateMachine("Idle-State");
            layer.EntryTransitionsTo(idleSubStateMachine).When(notMoveCondition);
            entryNullState.TransitionsTo(idleSubStateMachine).When(notMoveCondition);
            {
                var idleAnimation = aac.NewClip().Looping().Animating(clip =>
                {
                    moveScaleInit(clip);
                    constraintSwapGrounded(clip);
                    offsetInit(clip);

                    getOffsetZ(clip).WithFrameCountUnit(kf =>
                    {
                        var time = 0.0f;
                        kf.Constant(time, pprs.NeutralOffset);
                        time += pprs.BlinkLoopKeyTime;

                        kf.Constant(time, pprs.BlinkOffset);
                        time += pprs.BlinkingKeyTime;
                        kf.Constant(time, pprs.NeutralOffset);
                        time += pprs.BlinkingKeyTime * 0.5f;
                        kf.Constant(time, pprs.BlinkOffset);
                        time += pprs.BlinkingKeyTime;
                        kf.Constant(time, pprs.NeutralOffset);
                    });
                });
                var idleState = idleSubStateMachine.NewState("Idle").WithAnimation(idleAnimation);
                idleSubStateMachine.WithDefaultState(idleState);


                var lipAAnimation = aac.NewClip().Animating(clip =>
                {
                    moveScaleInit(clip);
                    constraintSwapGrounded(clip);
                    offsetInit(clip);
                    getOffsetZ(clip).WithOneFrame(pprs.LipAOffset);
                });
                var lipAState = idleSubStateMachine.NewState("LipA").WithAnimation(lipAAnimation);

                var lipThreshold = pprs.LipSyncThreshold;
                idleState.TransitionsTo(lipAState).When(av3.Voice.IsGreaterThan(lipThreshold));
                lipAState.TransitionsTo(idleState).When(av3.Voice.IsLessThan(lipThreshold));

                idleState.Exits().When(moveCondition).Or().When(notGround);
                lipAState.Exits().When(moveCondition).Or().When(notGround);
            }


            var movementSubStateMachine = layer.NewSubStateMachine("Move-State");
            layer.EntryTransitionsTo(movementSubStateMachine).When(moveCondition).Or().When(notGround);
            entryNullState.TransitionsTo(movementSubStateMachine).When(moveCondition).Or().When(notGround);
            {
                var enterFlyingAnimation = aac.NewClip().Animating(clip =>
                {
                    offsetInit(clip);
                    getOffsetZ(clip).WithFrameCountUnit(kf =>
                    {
                        kf.Constant(0.0f, pprs.NeutralOffset)
                        .Constant(pprs.MovingTranslationKeyTime * 0.5f, pprs.FlyOffset)
                        .Constant(pprs.MovingTranslationKeyTime * 1.0f, pprs.FlyOffset);
                    });

                    clip.Animates(pprs.GroundedPuniPuniConstraint, Constraint_GlobalWeight).WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 1.0f).Easing(pprs.MovingTranslationKeyTime, 0.0f);
                    });
                    clip.Animates(pprs.FlyingPuniPuniConstraint, Constraint_GlobalWeight).WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 0.0f).Easing(pprs.MovingTranslationKeyTime, 1.0f);
                    });


                    clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.x").WithOneFrame(1f);
                    var tfY = clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.y");
                    clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.z").WithOneFrame(1f);

                    tfY.WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 1.0f)
                        .Easing(pprs.MovingTranslationKeyTime * 0.3f, Mathf.Max(1.0f - pprs.MovementPuniPuniScale, 0))
                        .Easing(pprs.MovingTranslationKeyTime * 0.6f, 1.0f + pprs.MovementPuniPuniScale)
                        .Easing(pprs.MovingTranslationKeyTime, 1.0f)
                        ;
                    });
                });
                var movingEnterState = movementSubStateMachine.NewState("Enter-Moving").WithAnimation(enterFlyingAnimation);


                var flyingAnimation = aac.NewClip().Animating(clip =>
                {
                    offsetInit(clip);
                    moveScaleInit(clip);
                    getOffsetZ(clip).WithOneFrame(pprs.FlyOffset);

                    clip.Animates(pprs.GroundedPuniPuniConstraint, Constraint_GlobalWeight).WithOneFrame(0.0f);
                    clip.Animates(pprs.FlyingPuniPuniConstraint, Constraint_GlobalWeight).WithOneFrame(1.0f);
                });
                var movingState = movementSubStateMachine.NewState("Moving").WithAnimation(flyingAnimation);

                var exitFlyingAnimation = aac.NewClip().Animating(clip =>
                {
                    offsetInit(clip);
                    getOffsetZ(clip).WithFrameCountUnit(kf =>
                    {
                        kf.Constant(0.0f, pprs.FlyOffset)
                        .Constant(pprs.MovingTranslationKeyTime * 0.5f, pprs.FlyOffset)
                        .Constant(pprs.MovingTranslationKeyTime * 1.0f, pprs.NeutralOffset);
                    });

                    clip.Animates(pprs.GroundedPuniPuniConstraint, Constraint_GlobalWeight).WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 0.0f).Easing(pprs.MovingTranslationKeyTime, 1.0f);
                    });
                    clip.Animates(pprs.FlyingPuniPuniConstraint, Constraint_GlobalWeight).WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 1.0f).Easing(pprs.MovingTranslationKeyTime, 0.0f);
                    });


                    clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.x").WithOneFrame(1f);
                    var tfY = clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.y");
                    clip.Animates(pprs.MoveAnimationTransform, "m_LocalScale.z").WithOneFrame(1f);

                    tfY.WithFrameCountUnit(kf =>
                    {
                        kf.Easing(0.0f, 1.0f)
                        // .Easing(pprs.MovingTranslationKeyTime * 0.3f, 1.0f + pprs.MovementPuniPuniScale)
                        .Easing(pprs.MovingTranslationKeyTime * 0.6f, Mathf.Max(1.0f - pprs.MovementPuniPuniScale, 0))
                        .Easing(pprs.MovingTranslationKeyTime, 1.0f)
                        ;
                    });
                });
                var movingExitState = movementSubStateMachine.NewState("Exit-Moving").WithAnimation(exitFlyingAnimation);



                movementSubStateMachine.WithDefaultState(movingEnterState);
                movingEnterState.TransitionsTo(movingState).AfterAnimationFinishes();
                movingState.TransitionsTo(movingExitState).When(notMoveCondition).And(grounded);
                movingExitState.Exits().AfterAnimationFinishes();
            }

            idleSubStateMachine.TransitionsTo(movementSubStateMachine).When(moveCondition);
            movementSubStateMachine.TransitionsTo(idleSubStateMachine).When(notMoveCondition).And(grounded);
        }

        internal class NDMFContainerProvider : IAacAssetContainerProvider
        {
            private readonly BuildContext _ctx;
            public NDMFContainerProvider(BuildContext ctx) => _ctx = ctx;
            public void SaveAsPersistenceRequired(Object objectToAdd) => _ctx.AssetSaver.SaveAsset(objectToAdd);
            public void SaveAsRegular(Object objectToAdd) { }
            public void ClearPreviousAssets() { }
        }
    }
}
#endif
