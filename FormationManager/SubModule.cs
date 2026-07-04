using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;
using Bannerlord.UIExtenderEx;
using FormationManager.Data;
using FormationManager.Patches;
using System.Linq;

namespace FormationManager
{
    public class SubModule : MBSubModuleBase
    {
        private static Harmony? _harmony;
        private static UIExtender? _uiExtender;

        private static bool _uiExtenderInitialized;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            // Clear log from previous session so we start fresh
            try
            {
                string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                string logPath = System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs", "FormationManager", "log.txt");
                if (System.IO.File.Exists(logPath)) System.IO.File.Delete(logPath);
            }
            catch { }

            try
            {
                _harmony = new Harmony("com.formationmanager");
                _harmony.PatchAll();
                Logger.Log("[SubModule] Harmony.PatchAll() succeeded.");
            }
            catch (System.Exception ex)
            {
                Logger.Log($"[SubModule] Harmony.PatchAll() FAILED: {ex}");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!_uiExtenderInitialized)
            {
                _uiExtenderInitialized = true;
                try
                {
                    _uiExtender = UIExtender.Create("FormationManager");
                    _uiExtender.Register(typeof(SubModule).Assembly);
                    _uiExtender.Enable();
                }
                catch (System.Exception ex)
                {
                    try
                    {
                        string docs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                        string dir = System.IO.Path.Combine(docs, "Mount and Blade II Bannerlord", "Configs");
                        if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                        string path = System.IO.Path.Combine(dir, "FormationManager_LoadError.txt");
                        
                        string errorText = ex.ToString();
                        if (ex is System.Reflection.ReflectionTypeLoadException rtle)
                        {
                            errorText += "\nLoader Exceptions:\n";
                            foreach (var le in rtle.LoaderExceptions)
                            {
                                errorText += le.ToString() + "\n";
                            }
                        }
                        System.IO.File.WriteAllText(path, errorText);
                    }
                    catch
                    {
                        // Ignore secondary writing issues
                    }
                }
            }
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            // Natively register our custom BattleInitializationModel decorator
            try
            {
                var vanillaModel = gameStarter.Models.OfType<BattleInitializationModel>().FirstOrDefault();
                if (vanillaModel != null)
                {
                    gameStarter.AddModel(new Models.FormationManagerBattleInitializationModel(vanillaModel));
                    Logger.Log("[SubModule] Natively registered FormationManagerBattleInitializationModel successfully.");
                }
                else
                {
                    Logger.Log("[SubModule] Vanilla BattleInitializationModel not found to override.");
                }
            }
            catch (System.Exception ex)
            {
                Logger.Log($"[SubModule] Failed to natively register battle initialization model: {ex}");
            }
        }

        public override void OnGameLoaded(Game game, object initializerObject)
        {
            base.OnGameLoaded(game, initializerObject);
            if (game.GameType is Campaign)
            {
                var hero = TaleWorlds.CampaignSystem.Hero.MainHero;
                if (hero != null)
                    FormationAssignmentStore.Load(hero.StringId);
            }
        }

        public override void OnBeforeMissionBehaviorInitialize(Mission mission)
        {
            base.OnBeforeMissionBehaviorInitialize(mission);
            Logger.Log($"[SubModule] OnBeforeMissionBehaviorInitialize fired. Mission={mission?.GetType()?.Name}");
            if (MissionGuards.IsNavalMission(mission))
            {
                Logger.Log("[SubModule] Naval mission detected. Skipping FormationManager mission behavior.");
                return;
            }
            mission?.AddMissionBehavior(new MissionTroopReassignmentBehavior());
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            FormationAssignmentStore.Save();
        }
    }
}
