﻿using System.Collections.Generic;

namespace CooksAssistant
{
	public class Config
	{
		public bool CookingOverhaul { get; set; } = true;
		public bool CookingTakesTime { get; set; } = false;
		public bool PlayWithQuestline { get; set; } = true;
		public bool GiveLeftoversFromBigFoods { get; set; } = true;
		public bool MakeChangesToMaps { get; set; } = true;
		public bool MakeChangesToRecipes { get; set; } = true;
		public bool MakeChangesToIngredients { get; set; } = true;
		public bool DebugMode { get; set; } = true;
		public string ConsoleCommandPrefix { get; set; } = "cac";

		public string CookingStationUseRange { get; set; } = "2";
		public List<string> WhereToPutTheSaloonCookingStation = new List<string>
		{
			"16", "16"
		};
		public List<string> DefaultUnlockedRecipes = new List<string>
		{
			"Baked Potato"
		};
		public List<string> FoodsThatGiveLeftovers = new List<string>
		{
			"Seafood Sandwich",
			"Egg Sandwich",
			"Salad Sandwich",
			"Pizza",
			"Cake",
			"Chocolate Cake",
			"Pink Cake",
			"Watermelon"
		};
		public List<string> FoodsWithLeftoversGivenAsSlices = new List<string>
		{
			"pizza",
			"cake"
		};
	}
}