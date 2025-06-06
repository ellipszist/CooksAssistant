﻿using System;
using System.Collections.Generic;
using System.Linq;
using SpaceCore;
using StardewModdingAPI;
using StardewValley;

namespace LoveOfCooking.Objects
{
	public interface ICookingSkillAPI
	{
		public enum Profession // DO NOT EDIT
		{
			ImprovedSeasoning,
			Restoration,
			GiftBoost,
			SalePrice,
			ExtraPortion,
			BuffDuration
		}

		bool IsEnabled();
		CookingSkill GetSkill();
		int GetLevel();
		int GetMaximumLevel();
		IReadOnlyDictionary<ICookingSkillAPI.Profession, bool> GetCurrentProfessions(long playerID = -1L);
		bool HasProfession(ICookingSkillAPI.Profession profession, long playerID = -1L);
		bool AddExperienceDirectly(int experience);
		void AddCookingBuffToItem(string name, int value);
		int GetTotalCurrentExperience();
		int GetExperienceRequiredForLevel(int level);
		int GetTotalExperienceRequiredForLevel(int level);
		int GetExperienceRemainingUntilLevel(int level);
		IList<string> GetAllLoveOfCookingRecipes();
		IReadOnlyDictionary<int, IList<string>> GetAllLevelUpRecipes();
        IReadOnlyList<string> GetCookingRecipesForLevel(int level);
		int CalculateExperienceGainedFromCookingItem(Item item, int numIngredients, int numCooked, bool applyExperience);
		bool RollForExtraPortion();
	}

	public class CookingSkillAPI : ICookingSkillAPI
	{
		private readonly IReflectionHelper Reflection;

		public CookingSkillAPI(IReflectionHelper reflection)
		{
			this.Reflection = reflection;
		}

		/// <returns>Whether the Cooking skill is enabled in the mod config.</returns>
		public bool IsEnabled()
		{
			return ModEntry.Config.AddCookingSkillAndRecipes;
		}

		/// <returns>Instance of the Cooking skill.</returns>
		public CookingSkill GetSkill()
		{
			return SpaceCore.Skills.GetSkill(CookingSkill.InternalName) as CookingSkill;
		}

		/// <returns>Current base skill level.</returns>
		public int GetLevel()
		{
			return SpaceCore.Skills.GetSkillLevel(Game1.player, CookingSkill.InternalName);
		}

		/// <returns>Maximum possible skill level.</returns>
		public int GetMaximumLevel()
		{
			return this.GetSkill().ExperienceCurve.Length;
		}

		/// <returns>A dictionary of all possible Cooking professions and whether each is active.</returns>
		public IReadOnlyDictionary<ICookingSkillAPI.Profession, bool> GetCurrentProfessions(long playerID = -1L)
		{
			var dict = new Dictionary<ICookingSkillAPI.Profession, bool>();
			int count = Enum.GetNames(typeof(ICookingSkillAPI.Profession)).Length;
			for (int i = 0; i < count; ++i)
			{
				var profession = (ICookingSkillAPI.Profession)i;
				dict.Add(profession, this.HasProfession(profession: profession, playerID: playerID));
			}
			return dict;
		}

		/// <returns>Whether the player has unlocked and chosen the given profession.</returns>
		public bool HasProfession(ICookingSkillAPI.Profession profession, long playerID = -1L)
		{
			Farmer player = Game1.GetPlayer(playerID, onlyOnline: false) ?? Game1.MasterPlayer;
			return (player ?? Game1.player).HasCustomProfession(this.GetSkill().Professions[(int)profession]);
		}

		/// <summary>
		/// Not yet implemented
		/// </summary>
		/// <param name="name">Name of the item to receive the buff.</param>
		/// <param name="value">Added skill level amount, will act as a debuff if negative.</param>
		public void AddCookingBuffToItem(string name, int value)
		{
			if (value == 0)
				return;

			throw new NotImplementedException();
		}

		/// <summary>
		/// Add experience to the skill.
		/// Experience gains from cooking may be applied via CalculateExperienceGainedFromCookingItem. This method should not be used where CalculateExperience would be more appropriate.
		/// </summary>
		/// <returns>Whether the player gained a level from added experience.</returns>
		public bool AddExperienceDirectly(int experience)
		{
			Events.InvokeOnCookingExperienceGained(experienceGained: experience);

			int level = this.GetLevel();
			SpaceCore.Skills.AddExperience(Game1.player, CookingSkill.InternalName, experience);
			return level < this.GetLevel();
		}

		/// <returns>Total accumulated experience.</returns>
		public int GetTotalCurrentExperience()
		{
			return SpaceCore.Skills.GetExperienceFor(Game1.player, CookingSkill.InternalName);
		}

		/// <returns>Experience required to reach this level from the previous level.</returns>
		public int GetExperienceRequiredForLevel(int level)
		{
			CookingSkill skill = this.GetSkill();
			return level > 0 && level <= skill.ExperienceCurve.Length
				? level switch
				{
					0 => 0,
					1 => skill.ExperienceCurve[level - 1],
					_ => skill.ExperienceCurve[level - 1] - skill.ExperienceCurve[level - 2]
				}
				: 0;
		}

		/// <returns>Accumulated experience required to reach this level from zero.</returns>
		public int GetTotalExperienceRequiredForLevel(int level)
		{
			CookingSkill skill = this.GetSkill();
			return level > 0 && level <= skill.ExperienceCurve.Length
				? skill.ExperienceCurve[level - 1]
				: 0;
		}

		/// <returns>
		/// Difference between total accumulated experience and experience required to reach level.
		/// Negative if level is lower than current skill level.
		/// </returns>
		public int GetExperienceRemainingUntilLevel(int level)
		{
			return this.GetTotalExperienceRequiredForLevel(level) - this.GetTotalCurrentExperience();
		}

        /// <returns>Table of recipes learned through leveling Cooking.</returns>
        public IList<string> GetAllLoveOfCookingRecipes()
        {
            return CraftingRecipe.cookingRecipes.Keys
				.Where(s => s.StartsWith(ModEntry.AssetPrefix))
				.ToList();
        }

        /// <returns>Table of recipes learned through leveling Cooking.</returns>
        public IReadOnlyDictionary<int, IList<string>> GetAllLevelUpRecipes()
        {
			IDictionary<int, IList<string>> recipes = new Dictionary<int, IList<string>>();
			for (int level = 0; level <= 10; ++level)
            {
                recipes.TryAdd(level, []);
            }
			if ((CraftingRecipe.cookingRecipes ?? DataLoader.CookingRecipes(Game1.content)) is Dictionary<string, string> data)
            {
                foreach ((string key, string value) in data)
                {
                    string[] requirements = ArgUtility.Get(value.Split('/'), 3).Split(' ');
                    if (requirements.Length > 1 && requirements[0] == CookingSkill.InternalName && int.TryParse(requirements[^1], out int level))
                    {
                        recipes.TryAdd(level, []);
                        recipes[level].Add(key);
                    }
                }
            }
			return (IReadOnlyDictionary<int, IList<string>>)recipes;
        }

        /// <returns>New recipes learned when reaching this level.</returns>
        public IReadOnlyList<string> GetCookingRecipesForLevel(int level)
		{
			return (IReadOnlyList<string>)(this.GetAllLevelUpRecipes().TryGetValue(level, out var value) ? value ?? [] : []);
		}

		/// <summary>
		/// Calculates the experience to be gained from a certain food, and applies the gained experience to the Cooking skill if needed.
		/// </summary>
		/// <param name="item">Item to be cooked, used to determine whether the recipe has been cooked before.</param>
		/// <param name="ingredientsCount">Number of ingredients used in the cooking recipe for this item.</param>
		/// <param name="apply">Whether to apply experience gains to the skill.</param>
		/// <returns>Experience gained.</returns>
		public int CalculateExperienceGainedFromCookingItem(Item item, int ingredientsCount, int numCooked, bool apply)
		{
			CookingSkill skill = this.GetSkill();

			// Reward players for cooking brand new recipes
			int newBonus = Game1.player.recipesCooked.ContainsKey(item.ItemId)
				? 0
				: ModEntry.Definitions.CookingSkillValues.ExperienceNewRecipeBonus;

			// Gain more experience for the first time cooking a meal each day
			int dailyBonus = ModEntry.Instance.States.Value.FoodCookedToday.ContainsKey(item.Name)
				? 0
				: ModEntry.Definitions.CookingSkillValues.ExperienceDailyBonus;

			if (!ModEntry.Instance.States.Value.FoodCookedToday.ContainsKey(item.Name))
				ModEntry.Instance.States.Value.FoodCookedToday[item.Name] = 0;

			// Gain less experience the more that the same food is cooked for this day
			// magic-ass numbas
			float stackBonus // (Quantity * (Rate of decay per quantity towards roughly 50%) / Some divisor for experience rate)
				= Math.Min(numCooked, ModEntry.Definitions.CookingSkillValues.MaxFoodStackPerDayForExperienceGains)
					* Math.Max(6f, 12f - ModEntry.Instance.States.Value.FoodCookedToday[item.Name]) / 8f;

			// Gain more experience for recipe complexity
			float ingredientsBonus = ModEntry.Definitions.CookingSkillValues.ExperienceIngredientsBaseValue
				+ (ingredientsCount * ModEntry.Definitions.CookingSkillValues.ExperienceIngredientsBonusScaling);
			float experienceFromIngredients = ModEntry.Definitions.CookingSkillValues.ExperienceIngredientsFinalBaseValue
				+ (ingredientsBonus * ModEntry.Definitions.CookingSkillValues.ExperienceIngredientsBonusFinalMultiplier);

			// Sum up experience
			int currentLevel = this.GetLevel();
			int finalExperience = 0;

			if (currentLevel >= skill.ExperienceCurve.Length)
			{
				Log.D($"No experience was applied: Skill is at max level.",
					ModEntry.Config.DebugMode);
			}
			else
			{
				int nextLevel = currentLevel + 1;
				int remainingExperience = this.GetExperienceRemainingUntilLevel(nextLevel);
				int requiredExperience = this.GetExperienceRequiredForLevel(nextLevel);
				int summedExperience = (int)(newBonus + dailyBonus + (experienceFromIngredients * stackBonus));
				summedExperience = (int)(summedExperience * ModEntry.Definitions.CookingSkillValues.ExperienceGlobalScaling);
				if (ModEntry.Config.DebugMode)
                {
					summedExperience = (int)(summedExperience * ModEntry.DebugGlobalExperienceRate);
				}
				finalExperience = skill.ExperienceCurve.Length - currentLevel == 1
					? Math.Min(remainingExperience, summedExperience)
					: summedExperience;
				Log.D($"Cooked up {item.Name} with {ingredientsCount} ingredients.",
					ModEntry.Config.DebugMode);

				if (finalExperience < 1)
				{
					Log.D($"No experience was applied: None gained.",
						ModEntry.Config.DebugMode);
				}
				else if (apply)
				{
					Log.D($"{Environment.NewLine}Experience until level {nextLevel}:"
						+ $" ({requiredExperience - remainingExperience}/{requiredExperience})"
						+ $"{Environment.NewLine}Total experience: ({this.GetTotalCurrentExperience()}/{this.GetTotalExperienceRequiredForLevel(nextLevel)})"
						+ $"{Environment.NewLine}+{finalExperience} experience!",
						ModEntry.Config.DebugMode);
					this.AddExperienceDirectly(finalExperience);
				}
				else
				{
					Log.D($"No experience was applied: Probe.",
						ModEntry.Config.DebugMode);
				}
			}

			return finalExperience;
		}

		public bool RollForExtraPortion()
		{
			return Game1.random.NextDouble() < ModEntry.Definitions.CookingSkillValues.ExtraPortionChance;
		}
	}
}
