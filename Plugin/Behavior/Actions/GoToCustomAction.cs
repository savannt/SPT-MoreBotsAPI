using DrakiaXYZ.BigBrain.Brains;
using EFT;
using UnityEngine;

namespace MoreBotsAPI.Behavior.Actions
{
    public struct CustomNavigationPoint
    {
        public Vector3 Position;
        public CustomNavigationPoint(Vector3 position) { Position = position; }
    }

    public abstract class GoToCustomAction : CustomLogic
    {
        private LookAround baseSteeringLogic;

        public GoToCustomAction(BotOwner botOwner) : base(botOwner)
        {
            baseSteeringLogic = new LookAround();
        }

        public override void Start()
        {
            BotOwner.AimingManager.CurrentAiming.LoseTarget();
            base.Start();
        }

        public override void Stop()
        {
            BotOwner.Mover.Sprint(false);
            base.Stop();
        }

        public override void Update(CustomLayer.ActionData data)
        {
            var point = GetGoToPoint();
            BotOwner.GoToPoint(point.Position, mustHaveWay: false);

            UpdateBotMovement();
            UpdateSteering();
        }

        public void UpdateBotMovement()
        {
            BotOwner.SetPose(1f);
            BotOwner.BotLay.GetUp(true);
            BotOwner.Mover.Sprint(false);
            BotOwner.SetTargetMoveSpeed(1f);
        }

        public void UpdateSteering()
        {
            BotOwner.Steering.LookToMovingDirection();
            baseSteeringLogic.Update(BotOwner);
        }

        public abstract CustomNavigationPoint GetGoToPoint();
    }
}
