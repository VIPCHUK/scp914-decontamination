using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Interfaces;
using Exiled.Events.Handlers;
using MEC;
using PlayerRoles;
using UnityEngine;

namespace ScpDecontamination
{
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;
        public string TargetDoorName { get; set; } = "914";
        public float EffectDuration { get; set; } = 2.5f;
        public float CheckInterval { get; set; } = 1.0f;
        public string CassieMessageOnStart { get; set; } = "pitch_0.15 .g4 .g4 pitch_1 Attention . .g3 decontamination sequence activated";
        public string CassieMessageOnEnd { get; set; } = "decontamination sequence complete . .g3 . all systems nominal";
        public List<RoleTypeId> ExcludedScps { get; set; } = new List<RoleTypeId> { RoleTypeId.Scp106 };
        public float InsideDotProductThreshold { get; set; } = -0.1f;
    }
    
    public class Plugin : Plugin<Config>
    {
        public override string Name => "ScpDecontamination";
        public override string Author => "honvert";
        public override Version Version => new Version(2, 6, 0);

        public static Plugin Instance { get; private set; }

        private CoroutineHandle _mainCoroutine;
        private bool _isProcessActive;
        public bool IsPermanentlyDisabled;
        
        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"Плагин {Name} версии {Version} включен.");
            _isProcessActive = false;
            Server.RoundStarted += OnRoundStarted;
            _mainCoroutine = Timing.RunCoroutine(MonitorStateCoroutine());
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_mainCoroutine);
            if (_isProcessActive)
            {
                Door door = Door.Get(Config.TargetDoorName);
                if (door != null)
                    door.ChangeLock(DoorLockType.None);

                Room chamber = Room.Get(RoomType.Lcz914);
                if (chamber != null)
                {
                    foreach (var player in chamber.Players)
                        player.DisableEffect(EffectType.Decontaminating);
                }
            }
            _isProcessActive = false;
            Server.RoundStarted -= OnRoundStarted;
            Instance = null;
            Log.Info($"Плагин {Name} выключен.");
            base.OnDisabled();
        }

        private void OnRoundStarted()
        {
            IsPermanentlyDisabled = false;
            Log.Info("Decontamination status reset for new round.");
        }

        public void SetDecontaminationDisabled(bool isDisabled)
        {
            IsPermanentlyDisabled = isDisabled;
            var status = isDisabled ? "disabled" : "enabled";
            Log.Info($"Decontamination {status} by command.");

            if (isDisabled && _isProcessActive)
            {
                _isProcessActive = false;
                Door door = Door.Get(Config.TargetDoorName);
                if (door != null)
                    door.ChangeLock(DoorLockType.None);

                Room chamber = Room.Get(RoomType.Lcz914);
                if (chamber != null)
                {
                    foreach (var player in chamber.Players)
                        player.DisableEffect(EffectType.Decontaminating);
                }

                if (!string.IsNullOrWhiteSpace(Config.CassieMessageOnEnd))
                    Cassie.Message(Config.CassieMessageOnEnd, isNoisy: true, isSubtitles: true);
            }
        }
        
        private bool IsPlayerTrulyInside(Player player, Door mainDoor)
        {
            if (player == null || mainDoor == null) return false;

            Vector3 doorPosition = mainDoor.Position;
            Vector3 doorForward = mainDoor.Transform.forward;
            Vector3 directionToPlayer = player.Position - doorPosition;
            float dotProduct = Vector3.Dot(directionToPlayer.normalized, doorForward);
            
            return dotProduct < Config.InsideDotProductThreshold;
        }

        private IEnumerator<float> MonitorStateCoroutine()
        {
            yield return Timing.WaitForSeconds(5f); 

            while (true)
            {
                yield return Timing.WaitForSeconds(Config.CheckInterval); 

                if (IsPermanentlyDisabled)
                    continue;

                if (Round.IsEnded)
                {
                    if (_isProcessActive) 
                    {
                        _isProcessActive = false;
                        Door doorOnRoundEnd = Door.Get(Config.TargetDoorName);
                        Room chamberOnRoundEnd = Room.Get(RoomType.Lcz914);
                        if (doorOnRoundEnd != null)
                            doorOnRoundEnd.ChangeLock(DoorLockType.None);
                        
                        if (chamberOnRoundEnd != null)
                        {
                            foreach (var p in chamberOnRoundEnd.Players)
                                p.DisableEffect(EffectType.Decontaminating);
                        }
                    }
                    continue;
                }

                try
                {
                    Door mainDoor = Door.Get(Config.TargetDoorName); 
                    Room chamberRoom = Room.Get(RoomType.Lcz914);

                    if (mainDoor == null || chamberRoom == null)
                    {
                        continue;
                    }
                    
                    List<Player> playersTrulyInsideMachine = new List<Player>();
                    foreach (var player in chamberRoom.Players)
                    {
                        if (IsPlayerTrulyInside(player, mainDoor))
                        {
                            playersTrulyInsideMachine.Add(player);
                        }
                    }

                    bool isScpTrulyInsideMachine = playersTrulyInsideMachine.Any(p => p.IsScp && p.IsAlive && !Config.ExcludedScps.Contains(p.Role.Type));
                    bool isMainDoorClosed = mainDoor.IsFullyClosed; 
                    
                    if (isScpTrulyInsideMachine && isMainDoorClosed && !_isProcessActive)
                    {
                        _isProcessActive = true;
                        Log.Info($"SCP в закрытой камере SCP-914. Активация протокола.");
                        Timing.RunCoroutine(ActivationSequence(mainDoor));
                    }
                    else if ((!isScpTrulyInsideMachine || !isMainDoorClosed) && _isProcessActive)
                    {
                        _isProcessActive = false;
                        Log.Info($"Условия деактивации (SCP не внутри машины или дверь '{mainDoor.Name}' открыта). Деактивация протокола.");
                        
                        foreach (var player in playersTrulyInsideMachine)
                        {
                            player.DisableEffect(EffectType.Decontaminating);
                        }
                        
                        mainDoor.ChangeLock(DoorLockType.None); 
                        
                        if (!string.IsNullOrWhiteSpace(Config.CassieMessageOnEnd))
                            Cassie.Message(Config.CassieMessageOnEnd, isNoisy: true, isSubtitles: true);
                    }

                    if (_isProcessActive)
                    {
                        foreach (Player player in playersTrulyInsideMachine)
                        {
                            if (player.IsAlive)
                            {
                                player.EnableEffect(EffectType.Decontaminating, Config.EffectDuration, false);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Произошла ошибка в MonitorStateCoroutine: {e}");
                    if (_isProcessActive)
                    {
                        Door doorOnError = Door.Get(Config.TargetDoorName);
                        if (doorOnError!= null) doorOnError.ChangeLock(DoorLockType.None);
                        _isProcessActive = false;
                    }
                }
            }
        }

        private IEnumerator<float> ActivationSequence(Door doorToControl)
        {
            if (!string.IsNullOrWhiteSpace(Config.CassieMessageOnStart))
            {
                Cassie.Message(Config.CassieMessageOnStart, isNoisy: true, isSubtitles: true);
            }
            
            if (!doorToControl.IsFullyClosed)
            {
                doorToControl.IsOpen = false; 
                yield return Timing.WaitForSeconds(0.75f); 
            }
            
            doorToControl.ChangeLock(DoorLockType.AdminCommand);
            Log.Info($"Главная дверь '{doorToControl.Name}' (комната: {doorToControl.Room?.Type}) закрыта и заблокирована.");
        }
    }
}
