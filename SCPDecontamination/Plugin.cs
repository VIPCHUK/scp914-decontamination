using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Interfaces;
using MEC;
using PlayerRoles;

namespace ScpDecontamination
{
    public class Config : IConfig
    {
        [Description("Включен ли плагин?")]
        public bool IsEnabled { get; set; } = true;

        [Description("Включить ли отладочные сообщения в консоли? Помогает понять, почему плагин не срабатывает.")]
        public bool Debug { get; set; } = false;

        [Description("Имя главной двери камеры SCP-914, за которой нужно следить.")]
        public string TargetDoorName { get; set; } = "914";

        [Description("Длительность эффекта обеззараживания, применяемого каждый тик. Должна быть чуть больше интервала проверки.")]
        public float EffectDuration { get; set; } = 2.5f;

        [Description("Интервал в секундах, с которым плагин проверяет комнату.")]
        public float CheckInterval { get; set; } = 1.0f;

        [Description("Сообщение C.A.S.S.I.E. при АКТИВАЦИИ процесса обеззараживания.")]
        public string CassieMessageOnStart { get; set; } = "pitch_0.15 .g4 .g4 pitch_1 Attention . .g3 decontamination sequence activated";

        [Description("Сообщение C.A.S.S.I.E. при ЗАВЕРШЕНИИ процесса обеззараживания.")]
        public string CassieMessageOnEnd { get; set; } = "decontamination sequence complete . .g3 . all systems nominal";

        [Description("Список ролей SCP, на которых не будет срабатывать протокол обеззараживания.")]
        public List<RoleTypeId> ExcludedScps { get; set; } = new List<RoleTypeId> { RoleTypeId.Scp106 };
    }
    
    public class Plugin : Plugin<Config>
    {
        public override string Name => "ScpDecontamination";
        public override string Author => "honvert & Gemini";
        public override Version Version => new Version(2, 4, 0); // Версия обновлена

        public static Plugin Instance { get; private set; }

        private CoroutineHandle _mainCoroutine;
        private bool _isProcessActive;
        
        public override void OnEnabled()
        {
            Instance = this;
            Log.Info($"Плагин {Name} версии {Version} включен.");
            _isProcessActive = false; 
            _mainCoroutine = Timing.RunCoroutine(MonitorRoomCoroutine());
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Timing.KillCoroutines(_mainCoroutine);
            Instance = null;
            Log.Info($"Плагин {Name} выключен.");
            base.OnDisabled();
        }

        private IEnumerator<float> MonitorRoomCoroutine()
        {
            if (Config.Debug) Log.Debug("Главная корутина запущена.");
            yield return Timing.WaitForSeconds(5f); 

            while (true)
            {
                yield return Timing.WaitForSeconds(Config.CheckInterval); 

                try
                {
                    // ИСПРАВЛЕНИЕ: Получаем комнату и дверь отдельно для максимальной точности
                    Room chamberRoom = Room.Get(RoomType.Lcz914); 
                    Door mainDoor = Door.Get(Config.TargetDoorName); 

                    if (chamberRoom == null)
                    {
                        if (Config.Debug) Log.Warn($"[ОТЛАДКА] Не удалось найти комнату-камеру SCP-914 (Lcz914).");
                        continue;
                    }

                    if (mainDoor == null)
                    {
                        if (Config.Debug) Log.Warn($"[ОТЛАДКА] Не удалось найти главную дверь с именем '{Config.TargetDoorName}'.");
                        continue;
                    }

                    // --- Определяем условия ---
                    List<Player> playersInChamber = chamberRoom.Players.ToList();
                    bool isScpInChamber = playersInChamber.Any(p => p.IsScp && p.IsAlive && !Config.ExcludedScps.Contains(p.Role.Type));
                    
                    bool isDoorFullyClosed = mainDoor.IsFullyClosed;

                    if (Config.Debug) Log.Debug($"[ОТЛАДКА] Проверка состояния: SCP в камере: {isScpInChamber}, Дверь закрыта: {isDoorFullyClosed}, Процесс активен: {_isProcessActive}");

                    // --- Условие СТАРТА: SCP ВНУТРИ камеры, ГЛАВНАЯ ДВЕРЬ закрыта, и процесс еще не активен ---
                    if (isScpInChamber && isDoorFullyClosed && !_isProcessActive)
                    {
                        _isProcessActive = true;
                        Log.Info($"SCP в закрытой камере {chamberRoom.Type}. Активация протокола.");
                        Timing.RunCoroutine(ActivationSequence(mainDoor));
                    }
                    // --- Условие ОСТАНОВКИ: Процесс активен, но SCP вышел ИЛИ главная дверь открылась ---
                    else if ((!isScpInChamber || !isDoorFullyClosed) && _isProcessActive)
                    {
                        _isProcessActive = false;
                        Log.Info($"Условия деактивации выполнены (SCP покинул камеру или дверь открыта). Деактивация протокола.");
                        
                        // Снимаем эффект у всех игроков, которые могли быть в камере
                        foreach (var player in playersInChamber)
                        {
                            player.DisableEffect(EffectType.Decontaminating);
                        }
                        if (Config.Debug) Log.Debug("Эффект обеззараживания снят.");

                        mainDoor.ChangeLock(DoorLockType.None);
                        if (Config.Debug) Log.Debug($"Главная дверь '{mainDoor.Name}' разблокирована.");
                        
                        if (!string.IsNullOrWhiteSpace(Config.CassieMessageOnEnd))
                            Cassie.Message(Config.CassieMessageOnEnd, isNoisy: true, isSubtitles: true);
                    }

                    // --- Применение эффекта, если процесс активен ---
                    if (_isProcessActive)
                    {
                        if (Config.Debug) Log.Debug("Процесс активен. Применение эффектов.");
                        foreach (Player player in playersInChamber)
                        {
                            if (player.IsAlive)
                            {
                                player.EnableEffect(EffectType.Decontaminating, Config.EffectDuration, false);
                                if (Config.Debug) Log.Debug($"Применен эффект обеззараживания к {player.Nickname}.");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Произошла ошибка в MonitorRoomCoroutine: {e}");
                }
            }
        }

        private IEnumerator<float> ActivationSequence(Door doorToControl)
        {
            if (!string.IsNullOrWhiteSpace(Config.CassieMessageOnStart))
                Cassie.Message(Config.CassieMessageOnStart, isNoisy: true, isSubtitles: true);
            
            // Дверь уже должна быть закрыта по условию, поэтому мы ее только блокируем
            if (Config.Debug) Log.Debug($"Блокировка главной двери '{doorToControl.Name}'...");
            doorToControl.ChangeLock(DoorLockType.AdminCommand);
            
            Log.Info($"Главная дверь '{doorToControl.Name}' в комнате {doorToControl.Room.Type} заблокирована.");
            yield break;
        }
    }
}
