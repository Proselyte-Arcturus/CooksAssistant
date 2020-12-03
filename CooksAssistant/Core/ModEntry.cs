﻿using CooksAssistant.GameObjects;
using CooksAssistant.GameObjects.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using SpaceCore;
using SpaceCore.Events;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using xTile.Dimensions;
using Object = StardewValley.Object;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace CooksAssistant
{
	public class ModEntry : Mod
	{
		internal static ModEntry Instance;
		internal Config Config;
		internal ModSaveData SaveData;

		private bool _forceConfig = false;

		internal ITranslationHelper i18n => Helper.Translation;
		internal static IJsonAssetsApi JsonAssets;
		internal static Texture2D SpriteSheet;

		internal const string SaveDataKey = "SaveData";
		internal const string AssetPrefix = "blueberry.CooksAssistant.";
		internal const string ObjectPrefix = "blueberry.cac.";

		// Assets
		internal static readonly string BasicObjectsPack = Path.Combine("assets", "BasicObjectsPack");
		internal static readonly string NewRecipesPackPath = Path.Combine("assets", "NewRecipesPack");
		internal static readonly string NewCropsPackPath = Path.Combine("assets", "NewCropsPack");
		internal static readonly string NettlesPackPath = Path.Combine("assets", "NettlesPack");
		internal static readonly string SpriteSheetPath = Path.Combine("assets", "sprites");
		internal static readonly string MapTileSheetPath = Path.Combine("assets", "maptiles");
		internal static readonly string SkillIconPath = Path.Combine("assets", "skill");
		internal static readonly string LevelUpIconPath = Path.Combine("assets", "levelup");
		internal static readonly string BundleDataPath = Path.Combine("assets", "bundles");
		internal static readonly string BuffDataPath = Path.Combine("assets", "ingredientBuffChart");

		// Add Cooking Tool
		private const string CookingToolName = AssetPrefix + "tool";
		
		// Add Cooking Skill
		private static readonly Dictionary<int, string[]> CookingSkillLevelUpRecipes = new Dictionary<int, string[]>
		{
			{ 0, new[] { "Fried Egg", ObjectPrefix + "bakedpotato" } },
			{ 1, new[] { "burrito", "fritters" } },
			{ 2, new[] { "porridge", "breakfast" } },
			{ 3, new[] { "lobster", "loadedpotato", "stuffedpotato" } },
			{ 4, new[] { "cake", "hotcocoa", "waffles" } },
			{ 5, new[] { "mornay", "unagi" } },
			{ 6, new[] { "redberrypie", "cabbagepot", "stew" } },
			{ 7, new[] { "applepie", "skewers", "burger", "tropicalsalad" } },
			{ 8, new[] { "admiralpie", "dwarfstew", "roast", "hunters" } },
			{ 9, new[] { "gardenpie", "curry", "kebab" } },
			{ 10, new[] { "oceanplatter", "eggsando", "saladsando", "seafoodsando" } },
		};
		internal const string CookingSkillId = AssetPrefix + "CookingSkill";
		internal static readonly Dictionary<int, int> FoodCookedToday = new Dictionary<int, int>();
		internal const int MaxFoodStackPerDayForExperienceGains = 20;
		private const int CraftNettleTeaLevel = 3;
		private const int CraftCampfireLevel = 1;
		private Buff _watchingBuff;
		
		// Add Cooking to the Community Centre
		internal const string CommunityCentreAreaName = "Kitchen";
		internal const int CommunityCentreAreaNumber = 6;
		internal static readonly Rectangle CommunityCentreArea = new Rectangle(0, 0, 10, 11);
		internal static readonly Point CommunityCentreNotePosition = new Point(7, 6);
		// We use Linus' tent for the dummy area, since there's surely no conceivable way it'd be in the community centre
		internal static readonly Rectangle DummyOpenFridgeSpriteArea = new Rectangle(32, 560, 16, 32);
		internal static readonly Vector2 DummyFridgePosition = new Vector2(6830);
		internal Vector2 CommunityCentreFridgePosition = Vector2.Zero;
		internal int BundleStartIndex;
		private int _menuTab;

		// Add Cooking Questline
		internal const string ActionDockCrate = AssetPrefix + "DockCrate";
		internal const string ActionRange = AssetPrefix + "Range";
		internal const string DockCrateItem = "Pineapple";

		// Cook At Kitchens
		internal static readonly Location SaloonCookingRangePosition = new Location(18, 17);
		internal static Dictionary<string, string> NpcHomeLocations;
		
		// Food Can Burn
		private const string BurntFoodName = "Burnt Food";

		// Food Healing Takes Time
		private const float CombatRegenModifier = 0.02f;
		private const float CookingRegenModifier = 0.005f;
		private const float ForagingRegenModifier = 0.0012f;
		private float _healthOnLastTick, _staminaOnLastTick;
		private int _healthRegeneration, _staminaRegeneration;
		private uint _regenTicksCurr;
		private Queue<uint> _regenTicksDiff = new Queue<uint>();
		private Object _lastFoodEaten;
		private bool _lastFoodWasDrink;
		// debug
		private float _debugRegenRate;
		private uint _debugElapsedTime;

		// Play Cooking Animation
		public static readonly string[] SoupyFoods = new[]
		{
			"soup",
			"bisque",
			"chowder",
			"stew",
			"pot",
			"broth",
			"stock",
		};
		public static readonly string[] DrinkyFoods = new[]
		{
			"candy",
			"cocoa",
			"chocolate",
			"milkshake",
			"smoothie",
			"milk",
			"tea",
			"coffee",
			"espresso",
			"mocha",
			"latte",
			"cappucino",
			"drink",
		};
		public static readonly string[] SaladyFoods = new[]
		{
			"coleslaw",
			"salad",
			"lunch",
			"taco",
			"roll",
			"sashimi",
			"sushi",
			"sandwich",
			"unagi",
		};
		public static readonly string[] BakeyFoods = new[]
		{
			"cookie",
			"roast",
			"bake",
			"cupcake",
		};
		public static readonly string[] CakeyFoods = new[]
		{
			"bread",
			"bun",
			"cake",
			"cakes",
			"pie",
			"pudding",
			"bake",
			"biscuit",
			"brownie",
			"brownies",
			"cobbler",
			"cookie",
			"cookies",
			"crumble",
			"cupcake",
			"fingers",
			"muffin",
			"tart",
			"turnover",
		};
		public static readonly string[] PancakeyFoods = new[]
		{
			"pancake",
			"crepe",
			"hotcake"
		};
		public static readonly string[] PizzayFoods = new[]
		{
			"pizza",
			"pitta",
			"calzone",
			"tortilla",
		};

		// Others:
		internal static bool PlayerAgencyBlocked;
		private const string ChocolateName = ObjectPrefix + "chocolate";
		private const string NettlesName = ObjectPrefix + "nettle";
		private const string NettlesUsableMachine = "Keg";
		private const int NettlesUsableLevel = 2;
		// kebab
		private const string KebabBuffSource = AssetPrefix + "kebab";
		private const int KebabBonusDuration = 220;
		private const int KebabMalusDuration = 140;
		private const int KebabCombatBonus = 3;
		private const int KebabNonCombatBonus = 2;
		// configuration
		public static readonly List<int> IndoorsTileIndexesThatActAsCookingStations = new List<int>
		{
			498, 499, 632, 633
		};
		public static readonly List<string> FoodsThatGiveLeftovers = new List<string>
		{
			"Pizza",
			"Cake",
			"Chocolate Cake",
			"Pink Cake",
			ObjectPrefix + "seafoodsando",
			ObjectPrefix + "eggsando",
			ObjectPrefix + "saladsando",
			ObjectPrefix + "watermelon",
		};


		public override void Entry(IModHelper helper)
		{
			Instance = this;
			Config = helper.ReadConfig<Config>();
			if (_forceConfig)
			{
				Log.W("Forcing config setup.");
				ForceConfig();
			}

			// Asset editors
			var assetManager = new AssetManager();
			Helper.Content.AssetEditors.Add(assetManager);
			Helper.Content.AssetLoaders.Add(assetManager);
			SpriteSheet = Helper.Content.Load<Texture2D>($"{SpriteSheetPath}.png");
			
			// Game events
			Helper.Events.GameLoop.GameLaunched += GameLoopOnGameLaunched;
			Helper.Events.GameLoop.SaveLoaded += GameLoopOnSaveLoaded;
			Helper.Events.GameLoop.Saving += GameLoopOnSaving;
			Helper.Events.GameLoop.DayStarted += GameLoopOnDayStarted;
			Helper.Events.GameLoop.ReturnedToTitle += GameLoopOnReturnedToTitle;
			Helper.Events.GameLoop.UpdateTicked += GameLoopUpdateTicked;
			Helper.Events.Player.Warped += PlayerOnWarped;
			Helper.Events.Input.ButtonPressed += InputOnButtonPressed;
			Helper.Events.Display.MenuChanged += DisplayOnMenuChanged;

			if (Config.AddCookingSkill)
			{
				Skills.RegisterSkill(new CookingSkill());
			}
			if (Config.DebugMode && !_forceConfig)
			{
				Helper.Events.Display.RenderedHud += Event_DrawDebugHud;
			}
			SpaceEvents.OnItemEaten += SpaceEventsOnItemEaten;
			SpaceEvents.BeforeGiftGiven += SpaceEventsOnBeforeGiftGiven;

			// Harmony patches
			HarmonyPatches.Patch();
			
			// Console commands
			var cmd = Config.ConsoleCommandPrefix;
			Helper.ConsoleCommands.Add(cmd + "menu", "Open cooking menu.", (s, args) =>
			{
				if (!PlayerAgencyLostCheck())
					OpenNewCookingMenu(null);
			});
			Helper.ConsoleCommands.Add(cmd + "lvl", "Set cooking level.", (s, args) =>
			{
				if (!Config.AddCookingSkill)
				{
					Log.D("Cooking skill is not enabled.");
					return;
				}
				if (args.Length < 1)
					return;

				Skills.AddExperience(Game1.player, CookingSkillId,
					-1 * Skills.GetExperienceFor(Game1.player, CookingSkillId));
				for (var i = 0; i < int.Parse(args[0]); ++i)
					Skills.AddExperience(Game1.player, CookingSkillId, Skills.GetSkill(CookingSkillId).ExperienceCurve[i]);
				foreach (var profession in Skills.GetSkill(CookingSkillId).Professions)
					if (Game1.player.professions.Contains(profession.GetVanillaId()))
						Game1.player.professions.Remove(profession.GetVanillaId());
				Log.D($"Set Cooking skill to {Skills.GetSkillLevel(Game1.player, CookingSkillId)}");
			});
			Helper.ConsoleCommands.Add(cmd + "tool", "Set cooking tool level.", (s, args) =>
			{
				if (!Config.AddCookingTool)
				{
					Log.D("Cooking tool is not enabled.");
					return;
				}
				if (args.Length < 1)
					return;

				SaveData.CookingToolLevel = int.Parse(args[0]);
				Log.D($"Set Cooking tool to {SaveData.CookingToolLevel}");
			});
			Helper.ConsoleCommands.Add(cmd + "lvlmenu", "Show cooking level menu.", (s, args) =>
			{
				if (!Config.AddCookingSkill)
				{
					Log.D("Cooking skill is not enabled.");
					return;
				}
				Helper.Reflection.GetMethod(typeof(CookingSkill), "showLevelMenu").Invoke(
					null, new EventArgsShowNightEndMenus());
				Log.D("Bumped Cooking skill levelup menu.");
			});
			Helper.ConsoleCommands.Add(cmd + "tired", "Reduce health and stamina. Pass zero, one, or two values.", (s, args) =>
			{
				if (args.Length < 1)
				{
					Game1.player.health = Game1.player.maxHealth / 10;
					Game1.player.Stamina = Game1.player.MaxStamina / 10;
				}
				else
				{
					Game1.player.health = int.Parse(args[0]);
					Game1.player.Stamina = args.Length < 2 ? Game1.player.health * 2.5f : int.Parse(args[1]);
				}
				Log.D($"Set HP: {Game1.player.health}, EP: {Game1.player.Stamina}");
			});
			Helper.ConsoleCommands.Add(cmd + "recipes", "Show all unlocked player recipes.", (s, args) =>
			{
				var message = Game1.player.cookingRecipes.Keys.OrderBy(str => str).Aggregate("Cooking recipes:", (cur, str) => $"{cur}\n{str}");
				Log.D(message);
			});
			Helper.ConsoleCommands.Add(cmd + "anim", "Animate for generic or specific food.", (s, args) =>
			{
				CookingMenu.AnimateForRecipe(new CraftingRecipe(args.Length > 0 ? args[0] : "Fried Egg", true), 1, false);
			});
			Helper.ConsoleCommands.Add(cmd + "inv", "Print contents of current cooking menu inventory.", (s, args) =>
			{
				if (Game1.activeClickableMenu is CookingMenu menu)
				{
					Log.D(menu.inventory.actualInventory.Aggregate(
						$"INVENTORY: ({menu.inventory.actualInventory.Count})", (cur, item) => $"{cur}\n{item?.Name ?? "/////"}"));
				}
			});
			Helper.ConsoleCommands.Add(cmd + "unblock", "Unblock player movement.", (s, args) =>
			{
				PlayerAgencyBlocked = false;
			});
		}

		private void LoadJsonAssetsObjects()
		{
			JsonAssets = Helper.ModRegistry.GetApi<IJsonAssetsApi>("spacechase0.JsonAssets");
			if (JsonAssets == null)
			{
				Log.E("Can't access the Json Assets API. Is the mod installed correctly?");
				return;
			}
			JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, BasicObjectsPack));

			if (Config.AddCookingSkill)
			{
				Log.D("Did not add new recipes: Recipe additions are disabled in config file.");
			}
			else
			{
				JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, NewRecipesPackPath));
			}

			if (!Config.AddNewStuff)
			{
				Log.D("Did not add new objects: New stuff is disabled in config file.");
				return;
			}
			else if (Helper.ModRegistry.IsLoaded("PPJA.FruitsAndVeggies"))
			{
				Log.D("Did not add new crops: [PPJA] Fruits and Veggies already adds these objects.");
			}
			else
			{
				JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, NewCropsPackPath));
			}

			if (Helper.ModRegistry.IsLoaded("uberkwefty.wintercrops"))
			{
				Log.D("Did not add nettles: Winter Crops is enabled.");
			}
			else
			{
				JsonAssets.LoadAssets(Path.Combine(Helper.DirectoryPath, NettlesPackPath));
			}
		}

		private void ForceConfig()
		{
			Config.AddCookingMenu = true;
			Config.AddCookingCommunityCentreBundle = false;
			Config.AddCookingSkill = false;
			Config.AddCookingTool = false;
			Config.AddCookingQuestline = false;
			Config.AddNewStuff = false;
			Config.AddNewRecipeScaling = false;
			Config.PlayCookingAnimation = true;
			Config.FoodHealingTakesTime = false;
			Config.FoodCanBurn = false;
			Config.HideFoodBuffsUntilEaten = false;
			//Config.DebugMode = true;
			Config.DebugRegenTracker = false;
			Config.ConsoleCommandPrefix = "cac";
		}
		
		private void GameLoopOnGameLaunched(object sender, GameLaunchedEventArgs e)
		{
			PlayerAgencyBlocked = false;

			LoadJsonAssetsObjects();
		}

		private void GameLoopOnSaveLoaded(object sender, SaveLoadedEventArgs e)
		{
			PlayerAgencyBlocked = false;

			SaveData = Helper.Data.ReadSaveData<ModSaveData>(SaveDataKey) ?? new ModSaveData();

			// Invalidate and reload assets requiring JA indexes
			Helper.Content.InvalidateCache(@"Data/ObjectInformation");
			Helper.Content.InvalidateCache(@"Data/CookingRecipes");
			
			// Add watcher to check for first-time Kitchen bundle completion
			if (Config.AddCookingCommunityCentreBundle)
			{
				Helper.Content.InvalidateCache(@"Data/Bundles");
				Helper.Events.GameLoop.DayEnding += Event_WatchingKitchenBundle;
			}

			// Populate NPC home locations for cooking range usage
			//if (Config.CookAtKitchens)
			{
				var npcData = Game1.content.Load<Dictionary<string, string>>("Data/NPCDispositions");
				NpcHomeLocations = new Dictionary<string, string>();
				foreach (var npc in npcData)
					NpcHomeLocations.Add(npc.Key, npc.Value.Split('/')[10].Split(' ')[0]);
			}
		}
		
		private void GameLoopOnSaving(object sender, SavingEventArgs e)
		{
			PlayerAgencyBlocked = false;

			// TOOD: DEBUG: Reenable save data write
			//Helper.Data.WriteSaveData(SaveDataKey, SaveData);
		}

		private void GameLoopOnDayStarted(object sender, DayStartedEventArgs e)
		{
			PlayerAgencyBlocked = false;

			// Load starting recipes
			foreach (var recipe in CookingSkillLevelUpRecipes[0])
				if (!Game1.player.cookingRecipes.ContainsKey(recipe))
					Game1.player.cookingRecipes.Add(recipe, 0);
			var level = Skills.GetSkillLevel(Game1.player, CookingSkillId);
			if (Config.AddCookingSkill && level < CraftCampfireLevel)
			{
				Game1.player.craftingRecipes.Remove("Campfire");
			}

			// Clear daily cooking to free up Cooking experience gains
			if (Config.AddCookingSkill)
				FoodCookedToday.Clear();

			// Attempt to place a wild nettle as forage around other weeds
			if (Game1.currentSeason == "summer"
				|| ((Game1.currentSeason == "spring" || Game1.currentSeason == "fall") && Game1.dayOfMonth % 2 == 0))
			{
				foreach (var l in new[] {"Mountain", "Forest", "Railroad", "Farm"})
				{
					var location = Game1.getLocationFromName(l);
					var tile = location.getRandomTile();
					location.Objects.TryGetValue(tile, out var o);
					tile = Utility.getRandomAdjacentOpenTile(tile, location);
					if (tile == Vector2.Zero || o == null || o.ParentSheetIndex < 312 || o.ParentSheetIndex > 322)
						continue;
					location.terrainFeatures.Add(tile, new CustomBush(tile, location, CustomBush.BushVariety.Nettle));
				}
			}

			// aauugh
			if (Config.AddCookingCommunityCentreBundle)
			{
				UpdateCommunityCentreData(Game1.getLocationFromName("CommunityCenter") as CommunityCenter);
			}
		}

		private void GameLoopOnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
		{
			PlayerAgencyBlocked = false;

			// Clear food history
			_watchingBuff = null;
			_lastFoodEaten = null;
			_lastFoodWasDrink = false;

			// Cancel ongoing regeneration
			_regenTicksDiff.Clear();
			_regenTicksCurr = 0;
			_healthRegeneration = _staminaRegeneration = 0;
			_healthOnLastTick = _staminaOnLastTick = 0;
			_debugRegenRate = _debugElapsedTime = 0;

			// Remove Kitchen bundle watcher, assuming one exists
			if (Config.AddCookingCommunityCentreBundle)
				Helper.Events.GameLoop.DayEnding -= Event_WatchingKitchenBundle;
		}

		private void GameLoopUpdateTicked(object sender, UpdateTickedEventArgs e)
		{
			//Log.D($"UpdateTicked e.Ticks: {e.Ticks}");
			_healthOnLastTick = Game1.player.health;
			_staminaOnLastTick = Game1.player.Stamina;
		}
		
		private void Event_DrawDebugHud(object sender, RenderedHudEventArgs e)
		{
			for (var i = 0; i < _regenTicksDiff.Count; ++i)
				e.SpriteBatch.DrawString(
					Game1.smallFont,
					$"{(i == 0 ? "DIFF" : "      ")}   {_regenTicksDiff.ToArray()[_regenTicksDiff.Count - 1 - i]}",
					new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - 222, Game1.graphics.GraphicsDevice.Viewport.Height - 144 - i * 24),
					Color.White * ((_regenTicksDiff.Count - 1 - i + 1f) / (_regenTicksDiff.Count / 2f)));
			e.SpriteBatch.DrawString(
				Game1.smallFont,
				$"MOD  {(_debugRegenRate < 1 ? 0 :_debugElapsedTime % _debugRegenRate)}",
				new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - 222, Game1.graphics.GraphicsDevice.Viewport.Height - 120),
				Color.White);
			e.SpriteBatch.DrawString(
				Game1.smallFont,
				$"RATE {_debugRegenRate}",
				new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - 222, Game1.graphics.GraphicsDevice.Viewport.Height - 96),
				Color.White);
			e.SpriteBatch.DrawString(
				Game1.smallFont,
				$"HP+   {_healthRegeneration}",
				new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - 222, Game1.graphics.GraphicsDevice.Viewport.Height - 72),
				Color.White);
			e.SpriteBatch.DrawString(
				Game1.smallFont,
				$"EP+   {_staminaRegeneration}",
				new Vector2(Game1.graphics.GraphicsDevice.Viewport.Width - 222, Game1.graphics.GraphicsDevice.Viewport.Height - 48),
				Color.White);
		}

		private void Event_FoodRegeneration(object sender, UpdateTickedEventArgs e)
		{
			// TODO: TEST: Food regeneration rates at different health/stamina and skill levels (combat, foraging, cooking)
			if (PlayerAgencyLostCheck())
				return;
			if (Game1.player.health < 1 || _healthRegeneration < 1 && _staminaRegeneration < 1)
			{
				Helper.Events.GameLoop.UpdateTicked -= Event_FoodRegeneration;
				return;
			}

			var cookingLevel = Skills.GetSkillLevel(Game1.player, CookingSkillId);
			var baseRate = 128;
			var panicRate = (Game1.player.health * 3f + Game1.player.Stamina)
			                / (Game1.player.maxHealth * 3f + Game1.player.MaxStamina);
			var regenRate = GetFoodRegenRate(_lastFoodEaten);
			var scaling =
				(Game1.player.CombatLevel * CombatRegenModifier
				   + (Config.AddCookingSkill ? cookingLevel * CookingRegenModifier : 0)
				   + Game1.player.ForagingLevel * ForagingRegenModifier)
				/ (10 * CombatRegenModifier
				   + (Config.AddCookingSkill ? 10 * CookingRegenModifier : 0)
				   + 10 * ForagingRegenModifier);
			var rate = (baseRate - baseRate * scaling) * regenRate * 100d;
			rate = Math.Floor(Math.Max(32 - cookingLevel * 1.75f, rate * panicRate));

			_debugRegenRate = (float) rate;
			_debugElapsedTime = e.Ticks;
			++_regenTicksCurr;

			if (_regenTicksCurr < rate)
				return;

			_regenTicksDiff.Enqueue(_regenTicksCurr);
			if (_regenTicksDiff.Count > 5)
				_regenTicksDiff.Dequeue();
			_regenTicksCurr = 0;

			if (_healthRegeneration > 0)
			{
				if (Game1.player.health < Game1.player.maxHealth)
					++Game1.player.health;
				--_healthRegeneration;
			}

			if (_staminaRegeneration > 0)
			{
				if (Game1.player.Stamina < Game1.player.MaxStamina)
					++Game1.player.Stamina;
				--_staminaRegeneration;
			}
		}
		
		private void Event_WatchingToolUpgrades(object sender, UpdateTickedEventArgs e)
		{
			// Checks for purchasing a cooking tool upgrade from Clint's upgrade menu
			if (Game1.activeClickableMenu is ShopMenu menu
				&& menu.heldItem is StardewValley.Tools.GenericTool tool
				&& tool.Name.EndsWith(CookingToolName)
				&& tool.IndexOfMenuItemView - 17 < 3)
			{
				Game1.player.toolBeingUpgraded.Value = tool;
				Game1.player.daysLeftForToolUpgrade.Value = 2;
				Game1.playSound("parry");
				Game1.exitActiveMenu();
				Game1.drawDialogue(Game1.getCharacterFromName("Clint"),
					Game1.content.LoadString("Strings\\StringsFromCSFiles:Tool.cs.14317"));
			}

			// Checks for collecting your upgraded cooking tool from Clint after waiting the upgrade period
			if (Game1.player.mostRecentlyGrabbedItem is StardewValley.Tools.GenericTool tool1
				&& tool1.Name.EndsWith(CookingToolName)
				&& tool1.IndexOfMenuItemView - 17 > SaveData.CookingToolLevel - 1)
			{
				++SaveData.CookingToolLevel;
			}

			if (Game1.currentLocation.Name != "Blacksmith")
				Helper.Events.GameLoop.UpdateTicked -= Event_WatchingToolUpgrades;
		}

		private void Event_WatchingBuffs(object sender, UpdateTickedEventArgs e)
		{
			if (_watchingBuff == null
				|| (Game1.buffsDisplay.food?.source != _watchingBuff.source
					&& Game1.buffsDisplay.drink?.source != _watchingBuff.source
					&& Game1.buffsDisplay.otherBuffs.Any()
					&& Game1.buffsDisplay.otherBuffs.All(buff => buff?.source != _watchingBuff.source)))
			{
				Helper.Events.GameLoop.UpdateTicked -= Event_WatchingBuffs;

				_watchingBuff = null;
			}
		}
		
		private void Event_WatchingKitchenBundle(object sender, DayEndingEventArgs e)
		{
			// Send mail when completing the Kitchen in the community centre
			var mailId = $"cc{CommunityCentreAreaName}";
			var cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
			if (!cc.areasComplete[CommunityCentreAreaNumber] || Game1.player.mailReceived.Contains(mailId))
				return;

			Game1.player.mailForTomorrow.Add(mailId + "%&NL&%");
			Game1.addMailForTomorrow($"{Helper.ModRegistry.ModID}.ccKitchenBundleComplete");
		}

		private void Event_MoveJunimo(object sender, UpdateTickedEventArgs e)
		{
			var cc = Game1.currentLocation as CommunityCenter;
			var p = CommunityCentreNotePosition;
			if (cc.characters.FirstOrDefault(c => c is Junimo j && j.whichArea.Value == CommunityCentreAreaNumber)
			    == null)
			{
				Log.E($"No junimo in area {CommunityCentreAreaNumber} to move!");
			}
			else
			{
				cc.characters.FirstOrDefault(c => c is Junimo j && j.whichArea.Value == CommunityCentreAreaNumber)
					.Position = new Vector2(p.X, p.Y + 2) * 64f;
				Log.W("Moving junimo");
			}
			Helper.Events.GameLoop.UpdateTicked -= Event_MoveJunimo;
		}

		private void Event_ChangeJunimoMenuTab(object sender, UpdateTickedEventArgs e)
		{
			Helper.Reflection.GetField<int>((JunimoNoteMenu)Game1.activeClickableMenu, "whichArea").SetValue(_menuTab);
			if (_menuTab == CommunityCentreAreaNumber)
			{
				((JunimoNoteMenu)Game1.activeClickableMenu).bundles.Clear();
				((JunimoNoteMenu)Game1.activeClickableMenu).setUpMenu(
					CommunityCentreAreaNumber, ((CommunityCenter)Game1.getLocationFromName("CommunityCenter")).bundlesDict());
			}
			Helper.Events.GameLoop.UpdateTicked -= Event_ChangeJunimoMenuTab;
		}

		private void InputOnButtonPressed(object sender, ButtonPressedEventArgs e)
		{
			// Menu interactions
			if (e.Button.IsUseToolButton())
			{
				// Navigate community centre bundles inventory menu
				var cursor = Utility.Vector2ToPoint(e.Cursor.ScreenPixels);
				if (Config.AddCookingCommunityCentreBundle && Game1.activeClickableMenu is JunimoNoteMenu menu
					&& !((CommunityCenter)Game1.getLocationFromName("CommunityCenter")).isBundleComplete(CommunityCentreAreaNumber))
				{
					_menuTab = -1;
					var whichArea = Helper.Reflection.GetField<int>(menu, "whichArea");
					if (menu.areaBackButton != null && menu.areaBackButton.visible
							&& (menu.areaBackButton.containsPoint(cursor.X, cursor.Y) && whichArea.GetValue() == 0)
					   || (menu.areaNextButton != null && menu.areaNextButton.visible
							&& menu.areaNextButton.containsPoint(cursor.X, cursor.Y) && whichArea.GetValue() == 5))
					{
						_menuTab = CommunityCentreAreaNumber;
					}
					else if (whichArea.GetValue() == CommunityCentreAreaNumber)
					{
						if (menu.areaBackButton.visible && menu.areaBackButton.containsPoint(cursor.X, cursor.Y))
							_menuTab = 5;
						else if (menu.areaNextButton.visible && menu.areaNextButton.containsPoint(cursor.X, cursor.Y))
							_menuTab = 0;
					}
					if (_menuTab >= 0)
					{
						Log.D($"Changing JunimoNoteMenu whichArea from {whichArea.GetValue()} to {_menuTab}");
						Helper.Events.GameLoop.UpdateTicked += Event_ChangeJunimoMenuTab;
					}
				}
			}

			// World interactions
			if (PlayerAgencyLostCheck())
				return;

			// Debug hotkeys
			if (Config.DebugMode)
			{
				switch (e.Button)
				{
					case SButton.G:
						Game1.player.warpFarmer(Game1.currentLocation is CommunityCenter
							? new Warp(0, 0, "FarmHouse", 0, 0, false)
							: new Warp(0, 0, "CommunityCenter", 12, 6, false));
						return;
					case SButton.H:
						OpenNewCookingMenu(null);
						return;
					case SButton.F5:
						Game1.currentLocation.largeTerrainFeatures.Add(
							new Bush(e.Cursor.GrabTile, 1, Game1.currentLocation));
						return;
					case SButton.F6:
						Game1.currentLocation.terrainFeatures.Add(e.Cursor.GrabTile,
							new CustomBush(e.Cursor.GrabTile, Game1.currentLocation, CustomBush.BushVariety.Nettle));
						return;
					case SButton.F7:
						Game1.currentLocation.largeTerrainFeatures.Add(
							new CustomBush(e.Cursor.GrabTile, Game1.currentLocation, CustomBush.BushVariety.Redberry));
						return;
				}
			}

			// World interactions:
			if (Game1.currentBillboard != 0 || Game1.activeClickableMenu != null || Game1.menuUp // No menus
			    || !Game1.player.CanMove) // Player agency enabled
				return;

			var btn = e.Button;
			if (btn.IsActionButton())
			{
				// Try to open the new Cooking menu when nearby to cooking stations (ie. kitchen, range)
				var tile = Game1.currentLocation.Map.GetLayer("Buildings")
					.Tiles[(int)e.Cursor.GrabTile.X, (int)e.Cursor.GrabTile.Y];
				if (tile != null && IndoorsTileIndexesThatActAsCookingStations.Contains(tile.TileIndex))
				{
					if (NpcHomeLocations.Any(pair => pair.Value == Game1.currentLocation.Name
					                                 && Game1.player.getFriendshipHeartLevelForNPC(pair.Key) >= 5)
					|| NpcHomeLocations.All(pair => pair.Value != Game1.currentLocation.Name))
					{
						Log.W($"Clicked the kitchen at {Game1.currentLocation.Name}");
						OpenNewCookingMenu(null);
					}
					else
					{
						var name = NpcHomeLocations.FirstOrDefault(pair => pair.Value == Game1.currentLocation.Name).Key;
						Game1.showRedMessage(i18n.Get("world.range_npc.rejected",
							new {
								name = Game1.getCharacterFromName(name).displayName
							}));
					}
					Helper.Input.Suppress(e.Button);

					return;
				}

				// Open Community Centre fridge door
				if (Config.AddCookingCommunityCentreBundle && Game1.currentLocation is CommunityCenter cc
					&& tile != null && tile.TileIndex == 634)
				{
					CommunityCentreFridgePosition = e.Cursor.GrabTile;

					// Change tile to use custom open-fridge sprite
					Game1.currentLocation.Map.GetLayer("Front")
						.Tiles[(int)CommunityCentreFridgePosition.X, (int)CommunityCentreFridgePosition.Y - 1]
						.TileIndex = 1122;
					Game1.currentLocation.Map.GetLayer("Buildings")
						.Tiles[(int)CommunityCentreFridgePosition.X, (int)CommunityCentreFridgePosition.Y]
						.TileIndex = 1154;

					if (!((CommunityCenter)Game1.currentLocation).Objects.ContainsKey(DummyFridgePosition))
					{
						((CommunityCenter)Game1.currentLocation).Objects.Add(
							DummyFridgePosition, new Chest(true, DummyFridgePosition));
					}

					// Open the fridge as a chest
					((Chest)cc.Objects[DummyFridgePosition]).fridge.Value = true;
					((Chest)cc.Objects[DummyFridgePosition]).checkForAction(Game1.player);
				}

				// Use tile actions in maps
				CheckTileAction(e.Cursor.GrabTile, Game1.currentLocation);
			}
			else if (btn.IsUseToolButton())
			{
				// Ignore Nettles used on Kegs to make Nettle Tea when Cooking skill level is too low
				if ((!Config.AddCookingSkill || Skills.GetSkillLevel(Game1.player, CookingSkillId) < NettlesUsableLevel)
					&& Game1.player.ActiveObject?.Name == NettlesName
					&& Game1.currentLocation.Objects[e.Cursor.GrabTile]?.Name == NettlesUsableMachine)
				{
					Helper.Input.Suppress(btn);
					Game1.playSound("cancel");
				}
			}
		}

		private void DisplayOnMenuChanged(object sender, MenuChangedEventArgs e)
		{
			// TODO: DEBUG: Maybe remove and reapply all cooking recipes for level-down testing?

			// Add new recipes on level-up for Cooking skill
			if (e.NewMenu is SpaceCore.Interface.SkillLevelUpMenu levelUpMenu)
			{
				var level = Skills.GetSkillLevel(Game1.player, CookingSkillId);
				var field = Helper.Reflection.GetField<List<CraftingRecipe>>(levelUpMenu, "newCraftingRecipes");
				var recipes = CookingSkillLevelUpRecipes[level].Where(recipe => !Game1.player.cookingRecipes.ContainsKey(ObjectPrefix + recipe))
					.ToList().ConvertAll(recipe => new CraftingRecipe(ObjectPrefix + recipe, true));
				//levelUpMenu.height = 64 * recipes.Count + 256 + levelUpMenu.extraInfoForLevel.Count * 64 * 3 / 4;
				foreach (var recipe in recipes)
				{
					Game1.player.cookingRecipes.Add(recipe.name, 0);
				}
				if (level == CraftCampfireLevel)
				{
					recipes.Insert(0, new CraftingRecipe("Campfire", false));
					//levelUpMenu.height += 64);
				}
				field.SetValue(recipes);
			}

			// Add new crops and objects to shop menus
			if (e.NewMenu is ShopMenu menu)
			{
				if (Game1.currentLocation is SeedShop)
				{
					SortSeedShopStock(ref menu);
				}
				else if (Game1.currentLocation is JojaMart)
				{
					var o = new Object(Vector2.Zero, JsonAssets.GetObjectId(ChocolateName), int.MaxValue);
					menu.itemPriceAndStock.Add(o, new [] {(int) (o.Price * Game1.MasterPlayer.difficultyModifier), int.MaxValue});
					menu.forSale.Insert(menu.forSale.FindIndex(i => i.Name == "Sugar"), o);
				}
			}

			// Upgrade cooking equipment at the blacksmith
			if (Config.AddCookingTool && Game1.currentLocation.Name == "Blacksmith")
			{
				var canUpgrade = CanFarmerUpgradeCookingEquipment();
				var level = SaveData.CookingToolLevel;
				if (canUpgrade)
				{
					if (e.NewMenu is ShopMenu upgradeMenu)
					{
						var toolName = string.Format(
							$"{Game1.content.LoadString("Strings\\StringsFromCSFiles:Tool.cs." + (14299 + level))}",
							i18n.Get("menu.cooking_equipment.name").ToString());
						var toolDescription = i18n.Get("menu.cooking_equipment.description", new { level = level + 2 }).ToString();
						var cookingTool = new StardewValley.Tools.GenericTool(
							toolName, toolDescription, level + 1, 17 + level, 17 + level);
						var price = Helper.Reflection.GetMethod(
							typeof(Utility), "priceForToolUpgradeLevel").Invoke<int>(level + 1);
						var index = Helper.Reflection.GetMethod(
							typeof(Utility), "indexOfExtraMaterialForToolUpgrade").Invoke<int>(level + 1);
						upgradeMenu.itemPriceAndStock.Add(cookingTool, new int[3] { price / 2, 1, index });
						upgradeMenu.forSale.Add(cookingTool);
					}
				}
			}

			// Open the new Cooking Menu as a substitute when a cooking CraftingPage is opened
			if (Config.AddCookingMenu && e.NewMenu is CraftingPage cm)
			{
				var cooking = Helper.Reflection.GetField<bool>(cm, "cooking").GetValue();
				if (cooking)
				{
					var recipePages = Helper.Reflection.GetField
						<List<Dictionary<ClickableTextureComponent, CraftingRecipe>>>(cm, "pagesOfCraftingRecipes").GetValue();
					cm.exitThisMenuNoSound();
					OpenNewCookingMenu(recipePages.SelectMany(page => page.Values).ToList());
				}
				return;
			}

			// Close Community Centre fridge door after use
			if (e.OldMenu is ItemGrabMenu && e.NewMenu == null
				&& Config.AddCookingCommunityCentreBundle && Game1.currentLocation is CommunityCenter cc)
			{
				cc.Map.GetLayer("Front")
					.Tiles[(int)CommunityCentreFridgePosition.X, (int)CommunityCentreFridgePosition.Y - 1]
					.TileIndex = 602;
				cc.Map.GetLayer("Buildings")
					.Tiles[(int)CommunityCentreFridgePosition.X, (int)CommunityCentreFridgePosition.Y]
					.TileIndex = 634;
			}
		}
		
		private void PlayerOnWarped(object sender, WarpedEventArgs e)
		{
			PlayerAgencyBlocked = false;

			if (Config.AddCookingTool && e.NewLocation.Name == "Blacksmith")
			{
				Helper.Events.GameLoop.UpdateTicked += Event_WatchingToolUpgrades;
			}

			if ((!(e.NewLocation is CommunityCenter) && e.OldLocation is CommunityCenter)
				|| !(e.OldLocation is CommunityCenter) && e.NewLocation is CommunityCenter)
			{
				Helper.Content.InvalidateCache(@"Maps/townInterior");
			}

			if (!(e.NewLocation is CommunityCenter) || !Config.AddCookingCommunityCentreBundle)
				return;
			
			Helper.Events.GameLoop.UpdateTicked += Event_MoveJunimo;
			const int num = CommunityCentreAreaNumber;
			var cc = e.NewLocation as CommunityCenter; // fgs fds
			var count = Helper.Reflection.GetField<NetArray<bool, NetBool>>(
				cc, nameof(cc.areasComplete)).GetValue();
			var complete = cc.areAllAreasComplete() || Game1.MasterPlayer.hasCompletedCommunityCenter();
			Log.D($"CC areasComplete count: {count}, complete: {complete}");
			
			if (complete)
			{
				var multiplayer = Helper.Reflection.GetField<Multiplayer>(typeof(Game1), "multiplayer").GetValue();
				multiplayer.broadcastSprites(
					Game1.currentLocation,
					new TemporaryAnimatedSprite(
						"LooseSprites\\Cursors", 
						new Rectangle(354, 401, 7, 7), 
						9999, 1, 9999, 
						new Vector2(2096f, 344f), 
						false, false, 0.8f, 0f, Color.White,
						4f, 0f, 0f, 0f)
					{
						holdLastFrame = true
					});
				return;
			}

			var c1 = cc.isJunimoNoteAtArea(num);
			var c2 = cc.shouldNoteAppearInArea(num);
			if (!c1 && c2)
			{
				Log.E("Adding junimo note manually");
				cc.addJunimoNote(num);
				Helper.Reflection.GetMethod(cc, "resetSharedState").Invoke();
				Helper.Reflection.GetMethod(cc, "resetLocalState").Invoke();
			}
		}

		private void SpaceEventsOnItemEaten(object sender, EventArgs e)
		{
			if (!(Game1.player.itemToEat is Object food))
				return;

			var objectData = Game1.objectInformation[food.ParentSheetIndex].Split('/');
			_lastFoodWasDrink = objectData.Length > 6 && objectData[6] == "drink";
			_lastFoodEaten = food;

			Log.D($"Ate food: {food.Name}");
			Log.D($"Buffs: (food) {Game1.buffsDisplay.food?.displaySource} (drink) {Game1.buffsDisplay.drink?.displaySource}");
			if (Config.FoodHealingTakesTime)
			{
				Helper.Events.GameLoop.UpdateTicked += Event_FoodRegeneration;
				Game1.player.health = (int)_healthOnLastTick;
				Game1.player.Stamina = _staminaOnLastTick;
				_healthRegeneration += food.healthRecoveredOnConsumption();
				_staminaRegeneration += food.staminaRecoveredOnConsumption();
			}
			else if (Config.AddCookingSkill
			         && Game1.player.HasCustomProfession(Skills.GetSkill(CookingSkillId).Professions[(int) CookingSkill.ProfId.Restoration]))
			{
				Game1.player.health = (int) Math.Min(Game1.player.maxHealth,
					Game1.player.health + food.healthRecoveredOnConsumption() * (CookingSkill.RestorationAltValue / 100f));
				Game1.player.Stamina = (int) Math.Min(Game1.player.MaxStamina,
					Game1.player.Stamina + food.staminaRecoveredOnConsumption() * (CookingSkill.RestorationAltValue / 100f));
			}

			var lastBuff = _lastFoodWasDrink
				? Game1.buffsDisplay.drink
				: Game1.buffsDisplay.food;
			Log.D($"Last buff: {lastBuff?.displaySource ?? "null"} ({lastBuff?.source ?? "null"})"
			      + $" | Food: {food.DisplayName} ({food.Name})");
			if ((Config.AddCookingSkill
			    && Game1.player.HasCustomProfession(Skills.GetSkill(CookingSkillId).Professions[(int) CookingSkill.ProfId.BuffDuration]))
			    && food.displayName == lastBuff?.displaySource)
			{
				var duration = lastBuff.millisecondsDuration;
				if (duration > 0)
				{
					var rate = (Game1.player.health + Game1.player.Stamina)
					               / (Game1.player.maxHealth + Game1.player.MaxStamina);
					duration += (int) Math.Floor(CookingSkill.BuffDurationValue * 1000 * rate);
					lastBuff.millisecondsDuration = duration;
				}
			}

			_watchingBuff = lastBuff;

			if (!SaveData.FoodsEaten.ContainsKey(food.Name))
				SaveData.FoodsEaten.Add(food.Name, 0);
			++SaveData.FoodsEaten[food.Name];

			// Add leftovers from viable foods to the inventory, or drop it on the ground if full
			if (FoodsThatGiveLeftovers.Contains(food.Name))
			{
				var leftovers = new Object(
					JsonAssets.GetObjectId($"{food.Name}_half"), 1);
				if (Game1.player.couldInventoryAcceptThisItem(leftovers))
					Game1.player.addItemToInventory(leftovers);
				else
					Game1.createItemDebris(leftovers, Game1.player.Position, -1);
			}

			if (food.Name == "Kebab")
			{
				var roll = Game1.random.NextDouble();
				Buff buff = null;
				var duration = -1;
				var message = "";
				if (roll < 0.06f)
				{
					if (Config.FoodHealingTakesTime)
					{
						_healthRegeneration -= food.healthRecoveredOnConsumption();
						_staminaRegeneration -= food.staminaRecoveredOnConsumption();
					}
					else
					{
						Game1.player.health = (int)_healthOnLastTick;;
						Game1.player.Stamina = _staminaOnLastTick;
					}
					message = i18n.Get("item.kebab.bad");

					if (roll < 0.03f)
					{
						var stats = new[] {0, 0, 0, 0};
						stats[Game1.random.Next(stats.Length - 1)] = KebabNonCombatBonus * -1;

						message = i18n.Get("item.kebab.worst");
						var displaySource = i18n.Get("buff.kebab.inspect",
							new {quality = i18n.Get("buff.kebab.quality_worst")});
						duration = KebabMalusDuration;
						buff = roll < 0.0125f
							? new Buff(stats[0], stats[1], stats[2], 0, 0, stats[3],
								0, 0, 0, 0, 0, 0,
								duration, KebabBuffSource, displaySource)
							: new Buff(0, 0, 0, 0, 0, 0,
								0, 0, 0, 0,
								KebabCombatBonus * -1, KebabCombatBonus * -1,
								duration, KebabBuffSource, displaySource);
					}
				}
				else if (roll < 0.18f)
				{
					if (Config.FoodHealingTakesTime)
					{
						_healthRegeneration += Game1.player.maxHealth / 10;
						_staminaRegeneration += Game1.player.MaxStamina / 10;
					}
					else
					{
						Game1.player.health = Math.Min(Game1.player.maxHealth,
							Game1.player.health + Game1.player.maxHealth / 10);
						Game1.player.Stamina = Math.Min(Game1.player.MaxStamina,
							Game1.player.Stamina + Game1.player.MaxStamina / 10f);
					}

					var displaySource = i18n.Get("buff.kebab.inspect",
						new {quality = i18n.Get("buff.kebab.quality_best")});
					message = i18n.Get("item.kebab.best");
					duration = KebabBonusDuration;
					buff = new Buff(0, 0, KebabNonCombatBonus, 0, 0, 0,
						0, 0, 0, 0,
						KebabCombatBonus, KebabCombatBonus,
						duration, KebabBuffSource, displaySource);
				}
				if (string.IsNullOrEmpty(message))
					Game1.addHUDMessage(new HUDMessage(message));
				if (buff != null)
					Game1.buffsDisplay.tryToAddFoodBuff(buff, duration);
			}

			if (Config.AddCookingSkill
				&& ((!_lastFoodWasDrink && Game1.buffsDisplay.food?.source == food.Name)
					|| (_lastFoodWasDrink && Game1.buffsDisplay.drink?.source == food.Name)))
			{
				// TODO: SYSTEM: Cooking Skill with added levels
				((CookingSkill) Skills.GetSkill(CookingSkillId)).AddedLevel = 0;
				Helper.Events.GameLoop.UpdateTicked += Event_WatchingBuffs;
			}
		}
		
		private void SpaceEventsOnBeforeGiftGiven(object sender, EventArgsBeforeReceiveObject e)
		{
			// Ignore gifts that aren't going to be accepted
			if (!e.Npc.canReceiveThisItemAsGift(e.Gift)
				|| !Game1.player.friendshipData.ContainsKey(e.Npc.Name)
			    || Game1.player.friendshipData[e.Npc.Name].GiftsThisWeek > 1
			    || Game1.player.friendshipData[e.Npc.Name].GiftsToday > 0)
			{
				return;
			}

			// Cooking skill professions influence gift value of Cooking objects
			if (Config.AddCookingSkill
			    && Game1.player.HasCustomProfession(Skills.GetSkill(CookingSkillId).Professions[(int) CookingSkill.ProfId.GiftBoost])
			    && e.Gift.Category == -7)
			{
				Game1.player.changeFriendship(CookingSkill.GiftBoostValue, e.Npc);
			}
		}

		/// <summary>
		/// Checks whether the player has agency during gameplay, cutscenes, and while in menus.
		/// </summary>
		public bool PlayerAgencyLostCheck()
		{
			// HOUSE RULES
			return !Game1.game1.IsActive // No alt-tabbed game state
			       || Game1.eventUp && !Game1.currentLocation.currentEvent.playerControlSequence // No event cutscenes
			       || Game1.nameSelectUp || Game1.IsChatting || Game1.dialogueTyping || Game1.dialogueUp
				   || Game1.keyboardDispatcher.Subscriber != null // No text inputs
				   || Game1.player.UsingTool || Game1.pickingTool || Game1.numberOfSelectedItems != -1 // No tools in use
			       || Game1.fadeToBlack // None of that
				   || PlayerAgencyBlocked; // ESPECIALLY not that
		}

		public void CheckTileAction(Vector2 position, GameLocation location)
		{
			var property = location.doesTileHaveProperty(
				(int) position.X, (int) position.Y, "Action", "Buildings");
			if (property == null)
				return;
			var action = property.Split(' ');
			switch (action[0])
			{
				case ActionRange:
					// A new cooking range in the Saloon acts as a cooking station
					if (Config.AddCookingQuestline && Game1.player.getFriendshipHeartLevelForNPC("Gus") < 2)
					{
						CreateInspectDialogue(i18n.Get("world.range_gus.inspect"));
						break;
					}
					OpenNewCookingMenu(null);
					break;

				case ActionDockCrate:
					// Interact with the new crates at the secret beach pier to loot items for quests
					Game1.currentLocation.playSoundAt("ship", position);
					var roll = Game1.random.NextDouble();
					Object o = null;
					if (roll < 0.2f && Game1.player.eventsSeen.Contains(0))
					{
						o = new Object(JsonAssets.GetObjectId(DockCrateItem), 1);
						if (roll < 0.05f && Game1.player.eventsSeen.Contains(1))
							o = new Object(JsonAssets.GetObjectId(ChocolateName), 1);
					}
					if (o != null)
						Game1.player.addItemByMenuIfNecessary(o.getOne());
					break;
			}
		}
		
		/// <summary>
		/// I keep forgetting the method name
		/// </summary>
		private void CreateInspectDialogue(string dialogue)
		{
			Game1.drawDialogueNoTyping(dialogue);
		}
		
		private void OpenNewCookingMenu(List<CraftingRecipe> recipes)
		{
			Log.D("Opened cooking menu.");
			if (!(Game1.activeClickableMenu is CookingMenu)
			    || Game1.activeClickableMenu is CookingMenu menu && menu.PopMenuStack(true, true))
				Game1.activeClickableMenu = new CookingMenu(recipes);
		}
		
		/// <summary>
		/// Returns the base health/stamina regeneration rate for some food object.
		/// </summary>
		public float GetFoodRegenRate(Object food)
		{
			// Regen slower with drinks
			var rate = _lastFoodWasDrink ? 0.1f : 0.15f;
			// Regen faster with quality
			rate += food.Quality * 0.008f;
			// Regen faster when drunk
			if (Game1.player.hasBuff(17))
				rate *= 1.3f;
			if (Config.AddCookingSkill && Game1.player.HasCustomProfession(
				Skills.GetSkill(CookingSkillId).Professions[(int) CookingSkill.ProfId.Restoration]))
				rate += rate / CookingSkill.RestorationValue;
			return rate;
		}

		/// <summary>
		/// Identifies the level of the best cooking station within the player's use range.
		/// A cooking station's level influences the number of ingredients slots available to the player.
		/// </summary>
		/// <returns>Level of the best cooking station in range, defaults to 0.</returns>
		public int GetNearbyCookingStationLevel()
		{
			var toolLevel = SaveData.CookingToolLevel;
			var skillLevel = Skills.GetSkillLevel(Game1.player, CookingSkillId);
			var farmhouseLevel = GetFarmhouseKitchenLevel(Game1.getLocationFromName("FarmHouse") as FarmHouse);
			Log.D($"CheckForNearbyCookingStation()" +
				$"\nUsing Tool: {Config.AddCookingTool} Cooking: {skillLevel} Tool: {toolLevel}" +
				$" Farmer: {GetFarmersMaxUsableIngredients()} FarmHouse: {farmhouseLevel}");

			var cookingStationLevel = 0;
			var radius = 3;
			// Gus' cooking range uses his own equipment level as a baseline
			if (Game1.currentLocation.Name == "Saloon")
			{
				if (Utility.tileWithinRadiusOfPlayer(SaloonCookingRangePosition.X, SaloonCookingRangePosition.Y,
					radius, Game1.player))
				{
					cookingStationLevel = Math.Max(SaveData.SaloonCookingRangeLevel, GetFarmersMaxUsableIngredients());
					Log.W($"Cooking station: {cookingStationLevel}");
				}
			}
			// If indoors, use the farmhouse or cabin level as a base for cooking levels
			else if (!Game1.currentLocation.IsOutdoors)
			{
				var layer = Game1.currentLocation.Map.GetLayer("Buildings");
				var xLimit = Game1.player.getTileX() + radius;
				var yLimit = Game1.player.getTileY() + radius;
				for (var x = Game1.player.getTileX() - radius; x < xLimit && cookingStationLevel == 0; ++x)
				for (var y = Game1.player.getTileY() - radius; y < yLimit && cookingStationLevel == 0; ++y)
				{
					var tile = layer.Tiles[x, y];
					if (tile == null
					    || Game1.currentLocation.doesTileHaveProperty(x, y, "Action", "Buildings") != "kitchen" 
					    && !IndoorsTileIndexesThatActAsCookingStations.Contains(tile.TileIndex))
						continue;
					switch (Game1.currentLocation)
					{
						case FarmHouse farmHouse:
							// FarmHouses use their upgrade level as a baseline after Robin installs a kitchen
							cookingStationLevel = GetFarmhouseKitchenLevel(farmHouse);
							break;
						default:
							// NPC kitchens (other than the Saloon) use the Farmer's ingredients limits only
							cookingStationLevel = GetFarmersMaxUsableIngredients();
							break;
					}

					Log.W($"Cooking station: {Game1.currentLocation.Name}: Kitchen (level {cookingStationLevel})");
				}
			}
			else
			{
				var xLimit = Game1.player.getTileX() + radius;
				var yLimit = Game1.player.getTileY() + radius;
				for (var x = Game1.player.getTileX() - radius; x < xLimit && cookingStationLevel == 0; ++x)
				for (var y = Game1.player.getTileY() - radius; y < yLimit && cookingStationLevel == 0; ++y)
				{
					Game1.currentLocation.Objects.TryGetValue(new Vector2(x, y), out var o);
					if (o == null || o.Name != "Campfire")
						continue;
					cookingStationLevel = GetFarmersMaxUsableIngredients();
					Log.W($"Cooking station: {cookingStationLevel}");
				}
			}
			Log.W("Cooking station search finished");
			return cookingStationLevel;
		}

		/// <summary>
		/// Fetches the cooking station level for the farmhouse based on its upgrade/kitchen level,
		/// accounting for mods that would provide the kitchen at level 0.
		/// </summary>
		public int GetFarmhouseKitchenLevel(FarmHouse farmHouse)
		{
			var count = GetDefaultMaxUsableIngredients();
			if (count > 0)
			{
				return count;
			}

			// A basic (modded) farmhouse has a maximum of 1 slot,
			// and a farmhouse with a kitchen has a minimum of 2+ slots
			var level = farmHouse.upgradeLevel < 2
				? Math.Min(farmHouse.upgradeLevel, GetFarmersMaxUsableIngredients())
				: Math.Max(farmHouse.upgradeLevel, GetFarmersMaxUsableIngredients());
			// Thanks Lenne
			// TODO: TEST: Farmhouse mod cooking levels
			if (farmHouse.upgradeLevel == 0
				&& (Helper.ModRegistry.IsLoaded("Allayna.Kitchen")
					|| Helper.ModRegistry.IsLoaded("Froststar11.CustomFarmhouse")
					|| Helper.ModRegistry.IsLoaded("burakmese.products")
					|| Helper.ModRegistry.IsLoaded("minervamaga.FR.BiggerFarmhouses")))
			{
				level = 1;
			}
			return level;
		}

		public int GetFarmersMaxUsableIngredients()
		{
			var count = GetDefaultMaxUsableIngredients();
			if (count > 0)
			{
				return count;
			}
			return Config.AddCookingTool
					? 1 + SaveData.CookingToolLevel
					: 1 + Skills.GetSkillLevel(Game1.player, CookingSkillId) / 2;
		}

		public int GetDefaultMaxUsableIngredients()
		{
			return Config.AddCookingTool || Config.AddCookingSkill
				? -1
				//: Config.AddNewRecipeScaling ? 6 : 5;
				: 5;
		}

		private bool CanFarmerUpgradeCookingEquipment()
		{
			var results = new Dictionary<int, List<bool>>();
			bool canUpgrade(int skill, int tool)
			{
				return (skill / 2) / (tool + 1) >= 1;
			}
			void calculateLevelupTable()
			{
				for (var i = 0; i < 10; ++i)
				{
					var list = new List<bool>();
					for (var j = 0; j < 5; ++j)
					{
						var r = canUpgrade(i, j);
						list.Add(r);
					}
					results.Add(i, list);
				}
			}
			var skillLevel = Skills.GetSkillLevel(Game1.player, CookingSkillId);
			var toolLevel = SaveData.CookingToolLevel;
			var result = canUpgrade(skillLevel, toolLevel);
			//calculateLevelupTable();
			return result;
		}
		
		/// <summary>
		/// Bunches groups of common items together in the seed shop.
		/// Json Assets appends new stock to the bottom, and we don't want that very much at all.
		/// </summary>
		private void SortSeedShopStock(ref ShopMenu menu)
		{
			// Pair a suffix grouping some common items together with the name of the lowest-index (first-found) item in the group
			var itemList = menu.forSale;
			Log.D(itemList.Aggregate("Shop stock:", (total, cur) => $"{total}\n{cur.Name}"));
			var suffixes = new Dictionary<string, string>
				{{"seeds", null}, {"bulb", null}, {"starter", null}, {"Shoot", null}, {"sapling", null}};
			var debugCount = 0;
			for (var i = 0; i < itemList.Count; ++i)
			{
				// Ignore items without one of our group suffixes
				var suffix = suffixes.Keys.FirstOrDefault(s => itemList[i].Name.ToLower().EndsWith(s));
				if (suffix == null)
					continue;
				// Set the move-to-this-item name to be the first-found item in the group
				suffixes[suffix] ??= itemList[i].Name;
				if (suffixes[suffix] == itemList[i].Name)
					continue;
				// Move newly-found items of a group up to the first item in the group, and change the move-to name to this item
				var item = itemList[i];
				var index = 1 + itemList.FindIndex(i => i.Name == suffixes[suffix]);
				itemList.RemoveAt(i);
				itemList.Insert(index, item);
				suffixes[suffix] = itemList[index].Name;
				++debugCount;
				Log.D($"Moved {item.Name} to {itemList[index - 1].Name} at {index}");
			}
			Log.D($"Sorted seed shop stock, {debugCount} moves.");
			menu.forSale = itemList;
		}

		/// <summary>
		/// Updates multi-field entries separated by some delimiter, appending or replacing select fields.
		/// </summary>
		/// <returns>The old entry, with fields added from the new entry, reformed into a string of the delimited fields.</returns>
		public static string UpdateEntry(string oldEntry, string[] newEntry, bool append = false, bool replace = false,
			int startIndex = 0, char delimiter = '/')
		{
			var fields = oldEntry.Split(delimiter);
			if (replace)
				fields = newEntry;
			else for (var i = 0; i < newEntry.Length; ++i)
				if (newEntry[i] != null) 
					fields[startIndex + i] = append ? $"{fields[startIndex + i]} {newEntry[i]}" : newEntry[i];
			return SplitToString(fields, delimiter);
		}

		public static string SplitToString(IEnumerable<string> splitString, char delimiter = '/')
		{
			return splitString.Aggregate((cur, str) => $"{cur}{delimiter}{str}").Remove(0, 0);
		}
		
		/// <summary>
		/// god
		/// </summary>
		/// <param name="cc"></param>
		internal void UpdateCommunityCentreData(CommunityCenter cc)
		{
			Log.D("UpdateCommunityCentreData start");
			AppendAreasCompleteData(cc);// oh my god5
			AppendBundleData(cc);
			Log.D("UpdateCommunityCentreData end");
		}

		/// <summary>
		/// 
		/// </summary>
		internal void AppendAreasCompleteData(CommunityCenter cc)
		{
			try
			{
				Log.D("AppendAreasCompleteData");
				// fUCK YOUJ
				var areasComplete = Helper.Reflection
					.GetField<NetArray<bool, NetBool>>(cc, nameof(cc.areasComplete));
				var oldAreas = areasComplete.GetValue();
				var newAreas = new NetArray<bool, NetBool>(7);

				for (var i = 0; i < oldAreas.Count; ++i)
					newAreas[i] = oldAreas[i];
				newAreas[newAreas.Length - 1] = SaveData?.HasCompletedCookingBundle ?? false;
				areasComplete.SetValue(newAreas); // cunsn
			}
			catch (Exception e)
			{
				Log.E($"Exception in {nameof(AppendAreasCompleteData)}: {e}");
			}
		}

		/// <summary>
		/// This method is needed to update the CC bundle dictionary that's otherwise populated without our values.
		/// The CC constructor seemingly populates the dictionary without our changes to Data/Bundles, so it's topped up here.
		/// </summary>
		internal void AppendBundleData(CommunityCenter cc)
		{
			Log.D("AppendBundleData");

			var brokenDictField = Helper.Reflection.GetField<Dictionary<int, int>>(cc, "bundleToAreaDictionary");
			var brokenDict = brokenDictField.GetValue();
			var keys = Game1.netWorldState.Value.BundleRewards.Keys.Where
				(key => !brokenDict.ContainsKey(key) && key > BundleStartIndex && BundleStartIndex > 0);
			var keysDict = keys.ToDictionary(key => key, value => CommunityCentreAreaNumber);
			brokenDict = brokenDict.Concat(keysDict).ToDictionary(pair => pair.Key, pair => pair.Value);
			brokenDictField.SetValue(brokenDict);

			if (!Config.DebugMode)
				return;
			
			// aauugh
			var dog = Game1.netWorldState.Value.Bundles;
			var dogTreats = Game1.netWorldState.Value.BundleRewards;
			Log.D(dog.Aggregate("dog: ", (s, boolses)
				=> boolses?.Count > 0 ? $"{s}\n{boolses.Aggregate("", (s1, pair) => $"{s1}\n{pair.Key}: {pair.Value.Aggregate("", (s2, b) => $"{s2} {b}")}")}" : "none"));
			Log.D(dogTreats.Aggregate("dogTreats: ", (s, boolses)
				=> boolses?.Count > 0 ? $"{s}\n{boolses.Aggregate("", (s1, pair) => $"{s1}\n{pair.Key}: {pair.Value}")}" : "none"));
			Log.D(cc.areasComplete.Aggregate("AreasComplete: ", (s, b) => $"{s} {b}"));
			Log.D(brokenDict.Aggregate("bundleToAreaDictionary: ", (s, pair) => $"{s} ({pair.Key}:{pair.Value})"));
		}
	}
}
