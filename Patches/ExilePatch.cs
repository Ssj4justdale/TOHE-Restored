using AmongUs.Data;
using HarmonyLib;
using System.Linq;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

class ExileControllerWrapUpPatch
{
    public static GameData.PlayerInfo AntiBlackout_LastExiled;
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.WrapUp))]
    class BaseExileControllerPatch
    {
        public static void Postfix(ExileController __instance)
        {
            try
            {
                WrapUpPostfix(__instance.exiled);
            }
            finally
            {
                WrapUpFinalizer(__instance.exiled);
            }
        }
    }

    [HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.WrapUpAndSpawn))]
    class AirshipExileControllerPatch
    {
        public static void Postfix(AirshipExileController __instance)
        {
            try
            {
                WrapUpPostfix(__instance.exiled);
            }
            finally
            {
                WrapUpFinalizer(__instance.exiled);
            }
        }
    }
    static void WrapUpPostfix(GameData.PlayerInfo exiled)
    {
        if (AntiBlackout.OverrideExiledPlayer) exiled = AntiBlackout_LastExiled;

        bool DecidedWinner = false;
        if (!AmongUsClient.Instance.AmHost) return; //ホスト以外はこれ以降の処理を実行しません
        AntiBlackout.RestoreIsDead(doSend: false);
        if (!Collector.CollectorWin(false) && exiled != null) //判断集票者胜利
        {
            // Deal with the darkening bug for the spirit world
            if (!AntiBlackout.OverrideExiledPlayer && Main.ResetCamPlayerList.Contains(exiled.PlayerId))
                exiled.Object?.ResetPlayerCam(1f);

            exiled.IsDead = true;
            Main.PlayerStates[exiled.PlayerId].deathReason = PlayerState.DeathReason.Vote;
            
            var role = exiled.GetCustomRole();

            //判断冤罪师胜利
            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId))
            {
                if (!Options.InnocentCanWinByImp.GetBool() && role.IsImpostor())
                {
                    Logger.Info("冤罪的目标是内鬼，非常可惜啊", "Exeiled Winner Check");
                }
                else
                {
                    if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Innocent);
                    else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Innocent);
                    Main.AllPlayerControls.Where(x => x.Is(CustomRoles.Innocent) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == exiled.PlayerId)
                        .Do(x => CustomWinnerHolder.WinnerIds.Add(x.PlayerId));
                    DecidedWinner = true;
                }
            }
            foreach (var pc in Main.AllPlayerControls)
            //判断小丑胜利 (EAC封禁名单成为小丑达成胜利条件无法胜利)
            if (role == CustomRoles.Jester)
                {
                    if (DecidedWinner) CustomWinnerHolder.ShiftWinnerAndSetWinner(CustomWinner.Jester);
                    else CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Jester);
                    CustomWinnerHolder.WinnerIds.Add(exiled.PlayerId);
                    DecidedWinner = true;
                }

            //判断处刑人胜利
            if (Executioner.CheckExileTarget(exiled, DecidedWinner)) DecidedWinner = true;
            if (Lawyer.CheckExileTarget(exiled, DecidedWinner)) DecidedWinner = false;

            //判断恐怖分子胜利
            if (role == CustomRoles.Terrorist) Utils.CheckTerroristWin(exiled);

            if (role == CustomRoles.Devourer) Devourer.OnDevourerDied(exiled.PlayerId);

            if (CustomWinnerHolder.WinnerTeam != CustomWinner.Terrorist) Main.PlayerStates[exiled.PlayerId].SetDead();
        }
        if (AmongUsClient.Instance.AmHost && Main.IsFixedCooldown)
            Main.RefixCooldownDelay = Options.DefaultKillCooldown - 3f;

        Witch.RemoveSpelledPlayer();
        HexMaster.RemoveHexedPlayer();
        Occultist.RemoveCursedPlayer();

        foreach (var pc in Main.AllPlayerControls)
        {
            pc.ResetKillCooldown();
            if (Options.MayorHasPortableButton.GetBool() && pc.Is(CustomRoles.Mayor))
                pc.RpcResetAbilityCooldown();
            if (pc.Is(CustomRoles.Warlock))
            {
                Main.CursedPlayers[pc.PlayerId] = null;
                Main.isCurseAndKill[pc.PlayerId] = false;
                //RPC.RpcSyncCurseAndKill();
            }
            if (pc.GetCustomRole() is
                CustomRoles.Paranoia or
                CustomRoles.Veteran or
                CustomRoles.Greedier or
                CustomRoles.DovesOfNeace or
                CustomRoles.QuickShooter or
                CustomRoles.Addict or
                CustomRoles.ShapeshifterTOHE or
                CustomRoles.Wildling or
                CustomRoles.Twister or
                CustomRoles.Deathpact or
                CustomRoles.Dazzler or
                CustomRoles.Devourer or
                CustomRoles.Nuker or
                CustomRoles.Assassin or
                CustomRoles.Camouflager or
                CustomRoles.Disperser or
                CustomRoles.Escapee or
                CustomRoles.Hacker or
                CustomRoles.Hangman or
                CustomRoles.ImperiusCurse or
                CustomRoles.Miner or
                CustomRoles.Morphling or
                CustomRoles.Sniper or
                CustomRoles.Warlock or
                CustomRoles.Workaholic or
                CustomRoles.Chameleon or
                CustomRoles.Engineer or
                CustomRoles.Grenadier or
                CustomRoles.Scientist or
                CustomRoles.Lighter or
                CustomRoles.Pitfall or
                CustomRoles.ScientistTOHE or
                CustomRoles.Tracefinder or
                CustomRoles.Doctor or
                CustomRoles.Bomber
                ) pc.RpcResetAbilityCooldown();

            if (pc.Is(CustomRoles.Infected) && pc.IsAlive() && !CustomRoles.Infectious.RoleExist())
            {
                pc.RpcMurderPlayerV3(pc);
                Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
            }
            if (Main.ShroudList.ContainsKey(pc.PlayerId) && CustomRoles.Shroud.RoleExist())
            {
                pc.RpcMurderPlayerV3(pc);
                Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                Main.ShroudList.Clear();

            }

            Main.ShroudList.Clear();

            if (Options.RandomSpawn.GetBool() || Options.CurrentGameMode == CustomGameMode.SoloKombat)
            {
                RandomSpawn.SpawnMap map;
                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        map = new RandomSpawn.SkeldSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 1:
                        map = new RandomSpawn.MiraHQSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 2:
                        map = new RandomSpawn.PolusSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                }
            }

            FallFromLadder.Reset();
            Utils.CountAlivePlayers(true);
            Utils.AfterMeetingTasks();
            Utils.SyncAllSettings();
            Utils.NotifyRoles();
        }
    }

    static void WrapUpFinalizer(GameData.PlayerInfo exiled)
    {
        // Even if an exception occurs in WrapUpPostfix, this is the only part that will be executed reliably.
        if (AmongUsClient.Instance.AmHost)
        {
            _ = new LateTask(() =>
            {
                exiled = AntiBlackout_LastExiled;
                AntiBlackout.SendGameData();
                if (AntiBlackout.OverrideExiledPlayer && // State in which the expulsion target is overwritten (need not be executed if the expulsion target is not overwritten)
                    exiled != null && // exiled is not null
                    exiled.Object != null) //exiled.Object is not null
                {
                    exiled.Object.RpcExileV2();
                }
            }, 0.5f, "Restore IsDead Task");

            _ = new LateTask(() =>
            {
                Main.AfterMeetingDeathPlayers.Do(x =>
                {
                    var player = Utils.GetPlayerById(x.Key);
                    var state = Main.PlayerStates[x.Key];
                    Logger.Info($"{player.GetNameWithRole()} died with {x.Value}", "AfterMeetingDeath");
                    state.deathReason = x.Value;
                    state.SetDead();
                    player?.RpcExileV2();
                    if (x.Value == PlayerState.DeathReason.Suicide)
                        player?.SetRealKiller(player, true);
                    if (Main.ResetCamPlayerList.Contains(x.Key))
                        player?.ResetPlayerCam(1f);
                    Utils.AfterPlayerDeathTasks(player);
                });
                Main.AfterMeetingDeathPlayers.Clear();
            }, 0.5f, "AfterMeetingDeathPlayers Task");
        }

        GameStates.AlreadyDied |= !Utils.IsAllAlive;
        RemoveDisableDevicesPatch.UpdateDisableDevices();
        SoundManager.Instance.ChangeAmbienceVolume(DataManager.Settings.Audio.AmbienceVolume);
        Logger.Info("Start of Task Phase", "Phase");
    }

    [HarmonyPatch(typeof(PbExileController), nameof(PbExileController.PlayerSpin))]
    class PolusExileHatFixPatch
    {
        public static void Prefix(PbExileController __instance)
        {
            __instance.Player.cosmetics.hat.transform.localPosition = new(-0.2f, 0.6f, 1.1f);
        }
    }
}