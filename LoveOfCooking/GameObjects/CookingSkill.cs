﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceCore;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace LoveOfCooking.GameObjects
{
	public class CookingSkill : Skills.Skill
	{
		private static ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;
		public static readonly string Name = "Cooking";
		protected static readonly string ProfessionI18nId = "menu.cooking_skill.tier{0}_path{1}{2}";

		public enum ProfId
		{
			ImprovedOil,
			Restoration,
			GiftBoost,
			SaleValue,
			ExtraPortion,
			BuffDuration
		}

		internal static readonly int GiftBoostValue = 10;
		internal static readonly int SaleValue = 30;
		internal static readonly int ExtraPortionChance = 4;
		internal static readonly int RestorationValue = 35;
		internal static readonly int RestorationAltValue = 5;
		internal static readonly int BuffRateValue = 3;
		internal static readonly int BuffDurationValue = 36;

		internal static readonly float BurnChanceReduction = 0.015f;
		internal static readonly float BurnChanceModifier = 1.5f;

		internal int AddedLevel;

		public static readonly List<string> StartingRecipes = new List<string>
		{
			"Fried Egg",
			ModEntry.ObjectPrefix + "bakedpotato"
		};
		public static readonly Dictionary<int, List<string>> CookingSkillLevelUpRecipes = new Dictionary<int, List<string>>
		{
			{ 0, new List<string>() },
			{ 1, new List<string> { "burrito", "fritters" } },
			{ 2, new List<string> { "porridge", "breakfast" } },
			{ 3, new List<string> { "lobster", "loadedpotato", "stuffedpotato" } },
			{ 4, new List<string> { "cake", "hotcocoa", "waffles", "mornay" } },
			{ 5, new List<string>() },
			{ 6, new List<string> { "burger", "unagi", "cabbagepot", "stew" } }, // "redberrypie", 
			{ 7, new List<string> { "applepie", "eggsando", "skewers", "tropicalsalad" } }, // "burger", 
			{ 8, new List<string> { "admiralpie", "saladsando", "dwarfstew", "roast", "hunters" } },
			{ 9, new List<string> { "gardenpie", "seafoodsando", "curry", "kebab", "oceanplatter" } },
			{ 10, new List<string>() },
		};

		public class SkillProfession : Profession
		{
			public SkillProfession(Skills.Skill skill, string theId) : base(skill, theId) {}
	            
			internal string Name { get; set; }
			internal string Description { get; set; }
			public override string GetName() { return Name; }
			public override string GetDescription() { return Description; }
		}

		public CookingSkill() : base(Name)
		{
			Log.D($"Registering skill {Name}",
				ModEntry.Instance.Config.DebugMode);

			// Set experience values
			ExperienceCurve = new[] { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 }; // default
			ExperienceBarColor = new Color(57, 135, 214);

			// Set the skills page icon (cookpot)
			var size = 10;
			var texture = new Texture2D(Game1.graphics.GraphicsDevice, size, size);
			var pixels = new Color[size * size];
			ModEntry.SpriteSheet.GetData(0, new Rectangle(69, 220, size, size), pixels, 0, pixels.Length);
			texture.SetData(pixels);
			SkillsPageIcon = texture;

			// Set the skill level-up icon (pot on table)
			size = 16;
			texture = new Texture2D(Game1.graphics.GraphicsDevice, size, size);
			pixels = new Color[size * size];
			ModEntry.SpriteSheet.GetData(0, new Rectangle(0, 272, size, size), pixels, 0, pixels.Length);
			texture.SetData(pixels);
			Icon = texture;

			// Populate skill professions
			var textures = new Texture2D[6];
			for (var i = 0; i < textures.Length; ++i)
			{
				var x = 16 + (i * 16); // <-- Which profession icon to use is decided here
				ModEntry.SpriteSheet.GetData(0, new Rectangle(x, 272, size, size), pixels, 0, pixels.Length); // Pixel data copied from spritesheet
				textures[i] = new Texture2D(Game1.graphics.GraphicsDevice, size, size); // Unique texture created, no shared references
				textures[i].SetData(pixels); // Texture has pixel data applied

				// Set metadata for this profession
				var id = string.Format(ProfessionI18nId,
					i < 2 ? 1 : 2, // Tier
					i / 2 == 0 ? i + 1 : i / 2, // Path
					i < 2 ? "" : i % 2 == 0 ? "a" : "b"); // Choice
				var extra = i == 1 && !ModEntry.Instance.Config.FoodHealingTakesTime ? "_alt" : "";
				var profession = new SkillProfession(this, id)
				{
					Icon = textures[i], // <-- Skill profession icon is applied here
					Name = i18n.Get($"{id}{extra}.name"),
					Description = i18n.Get($"{id}{extra}.description", new {SaleValue, RestorationAltValue})
				};
				// Skill professions are paired and applied
				Professions.Add(profession);
				if (i > 0 && i % 2 == 1)
					ProfessionsForLevels.Add(new ProfessionPair(ProfessionsForLevels.Count == 0 ? 5 : 10,
						Professions[i - 1], Professions[i]));
			}
		}

		public override string GetName()
		{
			return ModEntry.AssetPrefix + "Cooking";
		}
		
		public override List<string> GetExtraLevelUpInfo(int level)
		{
			var list = new List<string>();
			if (ModEntry.Instance.Config.FoodCanBurn)
				list.Add(i18n.Get("menu.cooking_skill.levelup_burn", new { Number = level * BurnChanceModifier * BurnChanceReduction }));

			var extra = i18n.Get($"menu.cooking_skill.levelupbonus.{level}");
			if (extra.HasValue() && (level != ModEntry.CraftNettleTeaLevel || ModEntry.NettlesEnabled))
				list.Add(extra);

			return list;
		}

		public override string GetSkillPageHoverText(int level)
		{
			var str = "";

			if (ModEntry.Instance.Config.FoodCanBurn)
				str += "\n" + i18n.Get("menu.cooking_skill.levelup_burn", new { Number = level * BurnChanceModifier * BurnChanceReduction });

			return str;
		}

		public static List<string> GetNewCraftingRecipes(int level)
		{
			var list = new List<string>();
			for (var i = 0; i <= level; ++i)
			{
				list = list.Concat(CookingSkillLevelUpRecipes[i].Where(
					str => !Game1.player.cookingRecipes.ContainsKey(ModEntry.ObjectPrefix + str))).ToList();
			}
			for (var i = 0; i < list.Count; ++i)
			{
				list[i] = ModEntry.ObjectPrefix + list[i];
			}
			return list;
		}

		public static CookingSkill GetSkill()
		{
			return Skills.GetSkill(Name) as CookingSkill;
		}

		public static int GetLevel()
		{
			return Skills.GetSkillLevel(Game1.player, Name);
		}

		public static bool AddExperience(int experience)
		{
			var level = GetLevel();
			Skills.AddExperience(Game1.player, Name, experience);
			return level < GetLevel();
		}

		public static int GetTotalCurrentExperience()
		{
			return Skills.GetExperienceFor(Game1.player, Name);
		}

		public static int GetExperienceRequiredForNextLevel()
		{
			var level = GetLevel();
			return level > 0
				? GetSkill().ExperienceCurve[level] - GetSkill().ExperienceCurve[level - 1]
				: GetSkill().ExperienceCurve[0];
		}

		public static int GetTotalExperienceRequiredForNextLevel()
		{
			return GetSkill().ExperienceCurve[GetLevel()];
		}

		public static int GetExperienceRemainingUntilNextLevel()
		{
			return GetTotalExperienceRequiredForNextLevel() - GetTotalCurrentExperience();
		}
	}
}
