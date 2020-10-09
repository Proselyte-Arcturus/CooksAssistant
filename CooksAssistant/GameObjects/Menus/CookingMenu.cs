﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using Netcode;
using Object = StardewValley.Object;

namespace CooksAssistant.GameObjects.Menus
{
	public class CookingMenu : ItemGrabMenu
	{
		private static Texture2D Texture => ModEntry.SpriteSheet;
		private static ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;

		// Layout dimensions (variable with screen size)
		private static Rectangle _cookbookLeftRect = new Rectangle(-1, -1, 240 * 4 / 2, 128 * 4);
		private static Rectangle _cookbookRightRect = new Rectangle(-1, -1, 240 * 4 / 2, 128 * 4);
		private static Point _leftContent;
		private static Point _rightContent;
		private static int _lineWidth;
		private static int _textWidth;

		// Layout definitions
		private const int MarginLeft = 80;
		private const int MarginRight = 32;
		private const int PopupMargin = 8;
		private const int TextMuffinTopOverDivider = 6;
		private const int TextDividerGap = 4;

		private static readonly Color SubtextColour = Game1.textColor * 0.75f;
		private static readonly Color BlockedColour = Game1.textColor * 0.325f;
		private static readonly Color DividerColour = Game1.textColor * 0.325f;

		// Spritesheet source areas
		private static readonly Rectangle CookbookSource = new Rectangle(0, 80, 240, 128);
		private static readonly Rectangle CookingSlotOpenSource = new Rectangle(0, 208, 28, 28);
		private static readonly Rectangle CookingSlotLockedSource = new Rectangle(28, 208, 28, 28);
		private static readonly Rectangle NavRightButtonSource = new Rectangle(0, 192, 64, 64);
		private static readonly Rectangle NavLeftButtonSource = new Rectangle(0, 256, 64, 64);
		private static readonly Rectangle CookQuantityUpButtonSource = new Rectangle(177, 345, 7, 8);
		private static readonly Rectangle CookQuantityDownButtonSource = new Rectangle(184, 345, 7, 8);
		private static readonly Rectangle CookConfirmButtonSource = new Rectangle(128, 256, 64, 64);
		private static readonly Rectangle CookCancelButtonSource = new Rectangle(192, 256, 64, 64);
		private static readonly Rectangle CookButtonSource = new Rectangle(128, 0, 16, 22);
		
		private static readonly Dictionary<string, Rectangle> CookTextSource = new Dictionary<string, Rectangle>();
		private static readonly Point CookTextSourceOrigin = new Point(0, 240);
		private readonly Dictionary<string, int> CookTextSourceWidths;
		private const int CookTextSourceHeight = 16;
		private const int CookTextSideWidth = 5;
		private static int CookTextMiddleWidth;

		// Clickables
		private readonly ClickableTextureComponent NavRightButton;
		private readonly ClickableTextureComponent NavLeftButton;
		private readonly List<ClickableTextureComponent> CookingSlots = new List<ClickableTextureComponent>();
		private Rectangle CookButtonBounds;
		private ClickableTextureComponent CookQuantityUpButton;
		private ClickableTextureComponent CookQuantityDownButton;
		private ClickableTextureComponent CookConfirmButton;
		private ClickableTextureComponent CookCancelButton;

		// Text entry
		private readonly TextBox SearchBarTextBox;
		private readonly TextBox QuantityTextBox;

		// Menu data
		public enum State
		{
			Opening,
			Search,
			Recipe
		}
		private readonly Stack<State> _stack = new Stack<State>();
		private CraftingRecipe _currentRecipe;
		private Item _recipeItem;
		private string _recipeDescription;
		private Dictionary<int, int> _recipeIngredients;
		private List<int> _recipeBuffs;
		private int _cookingLevel;
		private List<Item> _cookingSlotsDropIn;
		private bool _showCookingConfirmationPopup;
		private int _craftableQuantity;

		// Animations
		private static readonly int[] _animTextOffsetPerFrame = { 0, 1, 0, -1, -2, -3, -2, -1 };
		private const int _animFrameTime = 100;
		private const int _animFrames = 8;
		private const int _animTimerLimit = _animFrameTime * _animFrames;
		private int _animTimer;
		private int _animFrame;

		// Others
		private readonly IReflectedField<Dictionary<int, double>> _iconShakeTimerField;

		// Testing
		private int _testRecipeIndex;
		private static readonly List<string> _testRecipes = new List<string>
		{
			"Pancakes", "Fiddlehead Risotto", "Red Plate", "Lobster Bisque", "Bread", "Pizza", "Seafoam Pudding"
		};

		private string _locale;
		private const int Scale = 4;

		public CookingMenu() : this(State.Opening) { }

		public CookingMenu(State state, string initialRecipe = null) : base(null)
		{
			_stack.Push(state);

			ModEntry.RemoveCookingMenuButton();
			Game1.displayHUD = false;
			_locale = LocalizedContentManager.CurrentLanguageCode.ToString();
			initializeUpperRightCloseButton();
			trashCan = null;
			//Game1.keyboardDispatcher.Subscriber = SearchBarTextBox;
			Game1.keyboardDispatcher.Subscriber = QuantityTextBox;

			_iconShakeTimerField = ModEntry.Instance.Helper.Reflection
				.GetField<Dictionary<int, double>>(inventory, "_iconShakeTimer");

			// Cooking ingredients item drop-in slots
			_cookingLevel = SpaceCore.Skills.GetSkillLevel(Game1.player, ModEntry.CookingSkillId) / 2; // nice
			_cookingLevel = 3; // nice
			_cookingSlotsDropIn = new List<Item>(_cookingLevel);
			CookingSlots.Clear();
			for (var i = 0; i < 5; ++i)
			{
				_cookingSlotsDropIn.Add(null);
				CookingSlots.Add(new ClickableTextureComponent(
					"cookingSlot" + i,
					new Rectangle(-1, -1, CookingSlotOpenSource.Width * Scale, CookingSlotOpenSource.Height * Scale),
					null, null, Texture, _cookingLevel <= i ? CookingSlotLockedSource : CookingSlotOpenSource, Scale));
			}

			// Clickables and elements
			NavRightButton = new ClickableTextureComponent(
				"navRight", new Rectangle(0, 0, 64, 64), null, null,
				Game1.mouseCursors, NavRightButtonSource, 1f);
			NavLeftButton = new ClickableTextureComponent(
				"navLeft", NavRightButton.bounds, null, null,
				NavRightButton.texture, NavLeftButtonSource, NavRightButton.scale);
			CookQuantityUpButton = new ClickableTextureComponent(
				"quantityUp", new Rectangle(-1, -1, 28, 32), null, null,
				Game1.mouseCursors, CookQuantityUpButtonSource, 4f);
			CookQuantityDownButton = new ClickableTextureComponent(
				"quantityDown", CookQuantityUpButton.bounds, null, null,
				Game1.mouseCursors, CookQuantityDownButtonSource, CookQuantityUpButton.scale);
			CookConfirmButton = new ClickableTextureComponent(
				"confirm", new Rectangle(-1, -1, 64, 64), null, null,
				Game1.mouseCursors, CookConfirmButtonSource, 1f);
			CookCancelButton = new ClickableTextureComponent(
				"confirm", CookConfirmButton.bounds, null, null,
				Game1.mouseCursors, CookCancelButtonSource, CookConfirmButton.scale);
			SearchBarTextBox = new TextBox(
				null, null, Game1.dialogueFont, Game1.textColor)
			{
				Text = i18n.Get("menu.cooking_recipe.search_label"),
				Selected = false
			};
			QuantityTextBox = new TextBox(
				Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
				null, Game1.smallFont, Game1.textColor)
			{
				numbersOnly = true,
				textLimit = 3,
				Selected = false,
				Text = " 1"
			};
			CookTextSourceWidths = new Dictionary<string, int>
			{
				{"en", 32},
				{"fr", 45},
				{"es", 42},
				{"pt", 48},
				{"jp", 50},
				{"zh", 36},
			};
			
			// 'Cook!' button localisations
			var xOffset = 0;
			var yOffset = 0;
			CookTextSource.Clear();
			foreach (var pair in CookTextSourceWidths)
			{
				if (xOffset + pair.Value > Texture.Width)
				{
					xOffset = 0;
					yOffset += CookTextSourceHeight;
				}
				CookTextSource.Add(pair.Key, new Rectangle(
					CookTextSourceOrigin.X + xOffset, CookTextSourceOrigin.Y + yOffset,
					pair.Value, CookTextSourceHeight));
				xOffset += pair.Value;
			}

			// Setup menu elements layout
			RealignElements();

			// Set menu to some random recipe for now
			_testRecipeIndex = Game1.random.Next(_testRecipes.Count);
			if (string.IsNullOrEmpty(initialRecipe))
				initialRecipe = _testRecipes[_testRecipeIndex];
			ChangeCurrentRecipe(initialRecipe);
		}

		private void RealignElements()
		{
			var view = Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea();
			var centre = Utility.PointToVector2(view.Center);

			// Menu
			xPositionOnScreen = (int)(centre.X - CookbookSource.Center.X * Scale);
			yPositionOnScreen = (int)(centre.Y - CookbookSource.Center.Y * Scale + 216);

			// Cookbook menu
			_cookbookLeftRect.X = xPositionOnScreen;
			_cookbookRightRect.X = _cookbookLeftRect.X + _cookbookLeftRect.Width;
			_cookbookLeftRect.Y = _cookbookRightRect.Y = yPositionOnScreen;

			_leftContent = new Point(_cookbookLeftRect.X + MarginLeft, _cookbookLeftRect.Y);
			_rightContent = new Point(_cookbookRightRect.X + MarginRight, _cookbookRightRect.Y);

			_lineWidth = _cookbookLeftRect.Width - MarginLeft * 3 / 2; // Actually mostly even for both Left and Right pages
			_textWidth = _lineWidth + TextMuffinTopOverDivider * 2;

			// Extra clickables
			upperRightCloseButton.bounds.X = xPositionOnScreen + CookbookSource.Width * Scale - 12;
			upperRightCloseButton.bounds.Y = yPositionOnScreen + 32;
			if (trashCan != null)
			{
				trashCan.bounds.X = xPositionOnScreen + CookbookSource.Width * Scale + 12;
				trashCan.bounds.Y = yPositionOnScreen + CookbookSource.Height * Scale - trashCan.bounds.Height - 96;
			}

			// Recipe nav buttons
			NavLeftButton.bounds.X = _leftContent.X - 24;
			NavRightButton.bounds.X = NavLeftButton.bounds.X + _lineWidth - 12;
			NavRightButton.bounds.Y = NavLeftButton.bounds.Y = _leftContent.Y + 20;

			// Ingredients item slots
			const int slotsPerRow = 3;
			var w = CookingSlots[0].bounds.Width;
			var h = CookingSlots[0].bounds.Height;
			var yOffset = 36;
			var xOffset = 0;
			var xOffsetExtra = 0;
			var extraSpace = (int)(w / 2f * (CookingSlots.Count % slotsPerRow) / 2f);
			for (var i = 0; i < CookingSlots.Count; ++i)
			{
				xOffset += w;
				if (i % slotsPerRow == 0)
				{
					if (i != 0)
						yOffset += h;
					xOffset = 0;
				}

				if (i == CookingSlots.Count - (CookingSlots.Count % slotsPerRow))
					xOffsetExtra = extraSpace;

				CookingSlots[i].bounds.X = _rightContent.X + xOffset + xOffsetExtra;
				CookingSlots[i].bounds.Y = _rightContent.Y + yOffset;
			}

			// Cook! button
			CookTextMiddleWidth = Math.Max(32, CookTextSource[_locale].Width);
			xOffset = _rightContent.X + _cookbookRightRect.Width / 2 - MarginRight;
			yOffset = _rightContent.Y + 344;
			CookButtonBounds = new Rectangle(
				xOffset - (CookTextSourceWidths[_locale] / 2 * Scale - CookTextSideWidth * Scale) + MarginLeft,
				yOffset,
				CookTextSideWidth * Scale * 2 + CookTextMiddleWidth * Scale, CookButtonSource.Height * Scale);

			// Cooking confirm buttons
			xOffset += 16;
			CookQuantityUpButton.bounds.X = CookQuantityDownButton.bounds.X = xOffset;
			CookQuantityUpButton.bounds.Y = yOffset - 12;
			
			var textSize = QuantityTextBox.Font.MeasureString(
				Game1.parseText("999", QuantityTextBox.Font, 96));
			QuantityTextBox.Text = " 1";
			QuantityTextBox.limitWidth = false;
			QuantityTextBox.Width = (int) textSize.X + 24;

			extraSpace = (QuantityTextBox.Width - CookQuantityUpButton.bounds.Width) / 2;
			QuantityTextBox.X = CookQuantityUpButton.bounds.X - extraSpace;
			QuantityTextBox.Y = CookQuantityUpButton.bounds.Y + CookQuantityUpButton.bounds.Height + 8;
			QuantityTextBox.Update();

			CookQuantityDownButton.bounds.Y = QuantityTextBox.Y + QuantityTextBox.Height + 4;

			CookConfirmButton.bounds.X = CookCancelButton.bounds.X
				= CookQuantityUpButton.bounds.X + CookQuantityUpButton.bounds.Width + extraSpace + 16;
			CookConfirmButton.bounds.Y = yOffset - 16;
			CookCancelButton.bounds.Y = CookConfirmButton.bounds.Y + CookConfirmButton.bounds.Height + 4;

			// Inventory
			inventory.xPositionOnScreen = xPositionOnScreen + CookbookSource.Width / 2 * Scale - inventory.width / 2;
			inventory.yPositionOnScreen = yPositionOnScreen + CookbookSource.Height * Scale + 8 - 20;

			// Inventory items
			yOffset = -4;
			var rowSize = inventory.capacity / inventory.rows;
			for (var i = 0; i < inventory.capacity; ++i)
			{
				if (i % rowSize == 0 && i != 0)
					yOffset += inventory.inventory[i].bounds.Height + 4;
				inventory.inventory[i].bounds.X = inventory.xPositionOnScreen + i % rowSize * inventory.inventory[i].bounds.Width;
				inventory.inventory[i].bounds.Y = inventory.yPositionOnScreen + yOffset;
			}
		}

		private void ChangeCurrentRecipe(string name)
		{
			_currentRecipe = new CraftingRecipe(name, true);
			_recipeItem = new Object(Game1.objectInformation.FirstOrDefault(
				pair => pair.Value.Split('/')[0] == name).Key, 1);
			_recipeIngredients = ModEntry.Instance.Helper.Reflection.GetField<Dictionary<int, int>>(
				_currentRecipe, "recipeList").GetValue();
			_recipeDescription = ModEntry.Instance.Helper.Reflection.GetField<string>(
				_currentRecipe, "description").GetValue();
			_recipeBuffs = Game1.objectInformation[_recipeItem.ParentSheetIndex]
				.Split('/')[7].Split(' ')
				.ToList().ConvertAll(int.Parse);
		}

		private void ReturnIngredientsToInventory()
		{
			if (_cookingSlotsDropIn.All(item => item == null))
				return;

			Log.W($"Trying to add {_cookingSlotsDropIn.Count(item => item != null)} ingredients from dropin slots");
			foreach (var item in _cookingSlotsDropIn)
				inventory.tryToAddItem(item);
			_cookingSlotsDropIn = new List<Item> { null, null, null, null, null };
		}

		public bool CanItemBeCooked(Item item)
		{
			return !(item is Tool || item is Furniture || item is Object o
				&& (o.bigCraftable.Value || o.specialItem || o.isLostItem || !o.canBeTrashed()));
		}

		public bool TryClickItem(int x, int y, bool moveEntireStack)
		{
			const string sound = "coin";
			var clickedAnItem = true;
			var inventoryItem = inventory.getItemAt(x, y);
			var inventoryIndex = inventory.getInventoryPositionOfClick(x, y);

			if (!CanItemBeCooked(inventoryItem))
			{
				inventory.ShakeItem(inventoryItem);
				Game1.playSound("cancel");
				return false;
			}

			var dropInIsFull = _cookingSlotsDropIn.GetRange(0, _cookingLevel).TrueForAll(i => i != null);

			// Add an inventory item to the ingredients dropIn slots in the best available position
			for (var i = 0; i < _cookingLevel && inventoryItem != null && clickedAnItem; ++i)
			{
				if (_cookingSlotsDropIn[i] == null || !_cookingSlotsDropIn[i].canStackWith(inventoryItem))
					continue;

				clickedAnItem = AddToIngredientsDropIn(
					inventoryIndex, i, moveEntireStack, false, sound) == 0;
			}
			// Try add inventory item to a new slot if it couldn't be stacked with any elements in dropIn ingredients slots
			if (inventoryItem != null && clickedAnItem)
			{
				// Ignore dropIn actions from inventory when ingredients slots are full
				var index = _cookingSlotsDropIn.FindIndex(i => i == null);
				if (dropInIsFull || index < 0)
				{
					//Game1.showRedMessage(i18n.Get("menu.cooking_recipe.locked"));
					inventory.ShakeItem(inventoryItem);
					Game1.playSound("cancel");
					return false;
				}
				clickedAnItem = AddToIngredientsDropIn(
					inventoryIndex, index, moveEntireStack, false, sound) == 0;
			}

			// Return a dropIn ingredient item to the inventory
			for (var i = 0; i < _cookingSlotsDropIn.Count && clickedAnItem; ++i)
			{
				if (!CookingSlots[i].containsPoint(x, y))
					continue;
				if (i >= _cookingLevel)
				{
					//Game1.showRedMessage(i18n.Get("menu.cooking_recipe.locked"));
					return false;
				}
				clickedAnItem = AddToIngredientsDropIn(
					inventoryIndex, i, moveEntireStack, true, sound) == 0;
			}

			return clickedAnItem;
		}

		/// <summary>
		/// Move quantities of stacks of two items, one in the inventory, and one in the ingredients dropIn.
		/// </summary>
		/// <param name="inventoryIndex">Index of item slot in the inventory to draw from.</param>
		/// <param name="ingredientsIndex">Index of item slot in the ingredients dropIn to add to.</param>
		/// <param name="moveEntireStack">If true, the quantity moved will be as large as possible.</param>
		/// <param name="reverse">If true, stack size from the ingredients dropIn is reduced, and added to the inventory.</param>
		/// <param name="sound">Name of sound effect to play when items are moved.</param>
		/// <returns>Quantity moved from one item stack to another. May return a negative number, affected by reverse.</returns>
		private int AddToIngredientsDropIn(int inventoryIndex, int ingredientsIndex,
			bool moveEntireStack, bool reverse, string sound = null)
		{
			// Add items to fill in empty slots at our indexes
			if (_cookingSlotsDropIn[ingredientsIndex] == null)
			{
				if (inventoryIndex == -1)
					return 0;

				_cookingSlotsDropIn[ingredientsIndex] = inventory.actualInventory[inventoryIndex].getOne();
				_cookingSlotsDropIn[ingredientsIndex].Stack = 0;
			}
			if (inventoryIndex == -1)
			{
				var dropOut = _cookingSlotsDropIn[ingredientsIndex].getOne();
				dropOut.Stack = 0;
				var item = inventory.actualInventory.FirstOrDefault(i => dropOut.canStackWith(i));
				inventoryIndex = inventory.actualInventory.IndexOf(item);
				if (item == null)
					inventory.actualInventory[inventoryIndex] = dropOut;
			}

			var addTo = !reverse
				? _cookingSlotsDropIn[ingredientsIndex]
				: inventory.actualInventory[inventoryIndex];
			var takeFrom = !reverse
				? inventory.actualInventory[inventoryIndex]
				: _cookingSlotsDropIn[ingredientsIndex];

			// Contextual goal quantity mimics the usual vanilla inventory dropIn interactions
			// (left-click moves entire stack, right-click moves one from stack, shift-right-click moves half the stack)
			var quantity = 0;
			if (addTo != null && takeFrom != null)
			{
				var max = addTo.maximumStackSize();
				quantity = moveEntireStack
					? takeFrom.Stack
					: Game1.isOneOfTheseKeysDown(Game1.oldKBState, new[] { new InputButton(Keys.LeftShift) })
						? (int)Math.Ceiling(takeFrom.Stack / 2.0)
						: 1;
				// Actual quantity is limited by the dest stack limit and source stack quantity
				quantity = Math.Min(quantity, max - addTo.Stack);
			}
			// If quantity is 0, we've probably reached these limits
			if (quantity == 0)
			{
				inventory.ShakeItem(inventory.actualInventory[inventoryIndex]);
				Game1.playSound("cancel");
			}
			// Add/subtract quantities from each stack, and remove items with empty stacks
			else
			{
				if (reverse)
					quantity *= -1;
				
				_cookingSlotsDropIn[ingredientsIndex].Stack += quantity;
				inventory.actualInventory[inventoryIndex].Stack -= quantity;
				if (_cookingSlotsDropIn[ingredientsIndex].Stack < 1)
					_cookingSlotsDropIn[ingredientsIndex] = null;
				if (inventory.actualInventory[inventoryIndex].Stack < 1)
					inventory.actualInventory[inventoryIndex] = null;
				
			}

			return quantity;
		}
		
		private void PopMenuStack(bool playSound, bool tryToQuit = false)
		{
			if (_stack.Count < 1)
				return;

			ReturnIngredientsToInventory();

			_stack.Pop();
			while (tryToQuit && _stack.Count > 0)
				_stack.Pop();

			if (playSound)
				Game1.playSound("bigDeSelect");

			if (!readyToClose() || _stack.Count > 0)
				return;
			Game1.exitActiveMenu();
			cleanupBeforeExit();
		}

		protected override void cleanupBeforeExit()
		{
			ReturnIngredientsToInventory();

			Game1.displayHUD = true;
			base.cleanupBeforeExit();
		}

		public override void snapToDefaultClickableComponent()
		{
			currentlySnappedComponent = getComponentWithID(0);
			snapCursorToCurrentSnappedComponent();
		}

		public override void performHoverAction(int x, int y)
		{
			if (_stack.Count < 1)
				return;

			hoveredItem = null;
			var obj = inventory.hover(x, y, heldItem);
			if (obj != null)
				hoveredItem = obj;
			for (var i = 0; i < _cookingSlotsDropIn.Count && hoveredItem == null; ++i)
				if (CookingSlots[i].containsPoint(x, y))
					hoveredItem = _cookingSlotsDropIn[i];

			if (trashCan != null)
			{
				if (trashCan.containsPoint(x, y))
				{
					if (trashCanLidRotation <= 0.0)
						Game1.playSound("trashcanlid");
					trashCanLidRotation = Math.Min(trashCanLidRotation + (float)Math.PI / 48f, 1.570796f);
					if (heldItem == null || Utility.getTrashReclamationPrice(heldItem, Game1.player) <= 0)
						return;
					hoverText = Game1.content.LoadString("Strings\\UI:TrashCanSale");
					hoverAmount = Utility.getTrashReclamationPrice(heldItem, Game1.player);
				}
				else
				{
					trashCanLidRotation = Math.Max(trashCanLidRotation - (float)Math.PI / 48f, 0.0f);
				}
			}

			upperRightCloseButton.tryHover(x, y, 0.5f);

			NavLeftButton.tryHover(x, y);
			NavRightButton.tryHover(x, y);

			CookQuantityUpButton.tryHover(x, y);
			CookQuantityDownButton.tryHover(x, y);
			QuantityTextBox.Hover(x, y);
			CookQuantityUpButton.tryHover(x, y, 0.5f);
			CookQuantityDownButton.tryHover(x, y, 0.5f);
			CookConfirmButton.tryHover(x, y);
			CookCancelButton.tryHover(x, y);
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
			if (_stack.Count < 1)
				return;

			//base.receiveLeftClick(x, y, playSound);

			if (Game1.activeClickableMenu == null)
				return;

			if (upperRightCloseButton.containsPoint(x, y))
			{
				PopMenuStack(false, true);
				return;
			}

			var state = _stack.Peek();

			if (NavLeftButton.containsPoint(x, y))
			{
				Game1.playSound("shwip");
				_testRecipeIndex = _testRecipeIndex - 1 == -1 ? _testRecipes.Count - 1 : --_testRecipeIndex;
				ChangeCurrentRecipe(_testRecipes[_testRecipeIndex]);
			}
			else if (NavRightButton.containsPoint(x, y))
			{
				Game1.playSound("shwip");
				_testRecipeIndex = _testRecipeIndex + 1 == _testRecipes.Count ? 0 : ++_testRecipeIndex;
				ChangeCurrentRecipe(_testRecipes[_testRecipeIndex]);
			}

			if (_showCookingConfirmationPopup)
			{
				var value = int.Parse(QuantityTextBox.Text);
				var max = _currentRecipe.getCraftableCount(_cookingSlotsDropIn);
				if (CookQuantityUpButton.containsPoint(x, y))
					++value;
				else if (CookQuantityDownButton.containsPoint(x, y))
					--value;
				QuantityTextBox.Text = Math.Max(1, Math.Min(max, value)).ToString();
				if (QuantityTextBox.Text.Length < 3)
					QuantityTextBox.Text = $" {QuantityTextBox.Text}";
			}
			QuantityTextBox.Update();

			TryClickItem(x, y, true);
		}

		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
			base.receiveRightClick(x, y, playSound);

			var shouldPop = TryClickItem(x, y, false);

			QuantityTextBox.Update();

			shouldPop = false;
			if (shouldPop)
				PopMenuStack(playSound);
		}

		public override void receiveGamePadButton(Buttons b)
		{
			Log.D($"receiveGamePadButton: {b.ToString()}");

			// TODO: SYSTEM: Keep GamePadButtons inputs up-to-date with KeyPress and Click behaviours

			if (b == Buttons.RightTrigger)
				return;
			else if (b == Buttons.LeftTrigger)
				return;
			else if (b == Buttons.B)
				PopMenuStack(true);
		}

		public override void receiveKeyPress(Keys key)
		{
			if (_stack.Count < 1)
				return;

			base.receiveKeyPress(key);

			var state = _stack.Peek();
			switch (state)
			{
				case State.Search:
				{
					// Navigate left/right buttons
					if (Game1.options.doesInputListContain(Game1.options.moveLeftButton, key))
						break;
					break;
				}
			}
			
			if (key == Keys.K)
			{
				_showCookingConfirmationPopup = !_showCookingConfirmationPopup;
			}
			else if (key == Keys.L)
			{
				var locales = CookTextSource.Keys.ToList();
				_locale = locales[(locales.IndexOf(_locale) + 1) % locales.Count];
				RealignElements();
			}
			else if (key == Keys.M)
			{
				Game1.activeClickableMenu = new NumberSelectionMenu(
					"asdadas", null, 300, 1, 5, 2);
			}

			if (QuantityTextBox.Selected && key == Keys.Enter)
			{
				QuantityTextBox.Selected = false;
			}

			if (Game1.options.doesInputListContain(Game1.options.menuButton, key)
				|| Game1.options.doesInputListContain(Game1.options.journalButton, key))
			{
				PopMenuStack(true);
			}

			if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && canExitOnKey)
			{
				PopMenuStack(true);
				if (Game1.currentLocation.currentEvent != null && Game1.currentLocation.currentEvent.CurrentCommand > 0)
					Game1.currentLocation.currentEvent.CurrentCommand++;
			}
			else if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && heldItem != null && trashCan != null)
			{
				Game1.setMousePosition(trashCan.bounds.Center);
			}
			if (key == Keys.Delete && heldItem != null && heldItem.canBeTrashed())
			{
				Utility.trashItem(heldItem);
				heldItem = null;
			}
		}

		public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
		{
			RealignElements();
			base.gameWindowSizeChanged(oldBounds, newBounds);
		}

		public override void update(GameTime time)
		{
			_animTimer += time.ElapsedGameTime.Milliseconds;
			if (_animTimer >= _animTimerLimit)
			{
				_animTimer = 0;
				if (false)
				_locale =
					CookTextSourceWidths.Keys.ToList()[
						(int)((time.TotalGameTime.TotalMilliseconds / _animTimerLimit / 3) % CookTextSourceWidths.Count)];
			}
			_animFrame = (int)((float)_animTimer / _animTimerLimit * _animFrames);

			base.update(time);
		}

		public override void draw(SpriteBatch b)
		{
			var duration = "30";

			var textPosition = Vector2.Zero;
			string text;
			int textWidth;

			///////////// BACKGROUND /////////////

			// Blackout
			b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.GetTitleSafeArea(),
				Color.Black * 0.5f);
			// Cookbook
			b.Draw(
				Texture,
				new Vector2(_cookbookLeftRect.X, _cookbookLeftRect.Y),
				CookbookSource,
				Color.White, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);

			///////////// LEFT PAGE /////////////

			// Clickables
			NavLeftButton.draw(b);
			NavRightButton.draw(b);

			// Recipe icon and title
			textPosition.Y = NavLeftButton.bounds.Y + 4;
			textPosition.X = NavLeftButton.bounds.X + NavLeftButton.bounds.Width;
			_currentRecipe.drawMenuView(b, (int)textPosition.X, (int)textPosition.Y);
			textWidth = 128;
			text = Game1.player.knowsRecipe(_currentRecipe.name)
				? _currentRecipe.DisplayName
				: i18n.Get("menu.cooking_recipe.title_unknown");
			textPosition.X = NavLeftButton.bounds.Width + 64;
			textPosition.Y -= Game1.smallFont.MeasureString(
				Game1.parseText(text, Game1.smallFont, textWidth)).Y / 2 - 24;
			DrawText(b, text, 1.5f, textPosition.X, textPosition.Y, textWidth, true);

			// Recipe description
			textPosition.X = 0;
			textPosition.Y = NavLeftButton.bounds.Y + NavLeftButton.bounds.Height + 20;
			textWidth = _textWidth;
			text = Game1.player.knowsRecipe(_currentRecipe.name)
				? _recipeDescription
				: i18n.Get("menu.cooking_recipe.title_unknown");
			DrawText(b, text, 1f, textPosition.X, textPosition.Y, textWidth, true);
			textPosition.Y += TextDividerGap * 2;

			// Recipe ingredients
			textPosition.Y += TextDividerGap + Game1.smallFont.MeasureString(
				Game1.parseText("Hoplite!\nHoplite!\nHoplite!", Game1.smallFont, textWidth)).Y;
			DrawHorizontalDivider(b, 0, textPosition.Y, _lineWidth, true);
			textPosition.Y += TextDividerGap;
			text = i18n.Get("menu.cooking_recipe.ingredients_label");
			DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, true, SubtextColour);
			textPosition.Y += Game1.smallFont.MeasureString(
				Game1.parseText(text, Game1.smallFont, textWidth)).Y;
			DrawHorizontalDivider(b, 0, textPosition.Y, _lineWidth, true);
			textPosition.Y += TextDividerGap - 64 / 2 + 4;

			if (Game1.player.knowsRecipe(_currentRecipe.name))
			{
				for (var i = 0; i < _recipeIngredients.Count; ++i)
				{
					textPosition.Y += 64 / 2 + 4;

					var id = _recipeIngredients.Keys.ElementAt(i);
					var requiredCount = _recipeIngredients.Values.ElementAt(i);
					var requiredItem = id;
					var bagCount = Game1.player.getItemCount(requiredItem, 8);
					requiredCount = _cookingSlotsDropIn.Where(item => item.ParentSheetIndex == id)
						.Aggregate(requiredCount - bagCount, (current, item) => current - item.Stack);
					var ingredientNameText = _currentRecipe.getNameFromIndex(id);
					var drawColour = requiredCount <= 0 ? Game1.textColor : BlockedColour;

					// Ingredient icon
					b.Draw(
						Game1.objectSpriteSheet,
						new Vector2(_leftContent.X, textPosition.Y - 2f),
						Game1.getSourceRectForStandardTileSheet(Game1.objectSpriteSheet,
							_currentRecipe.getSpriteIndexFromRawIndex(id), 16, 16),
						Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0.86f);
					// Ingredient quantity
					Utility.drawTinyDigits(
						_recipeIngredients.Values.ElementAt(i),
						b,
						new Vector2(
							_leftContent.X + 32 - Game1.tinyFont.MeasureString(
								string.Concat(_recipeIngredients.Values.ElementAt(i))).X,
							textPosition.Y + 21 - 2f),
						2f,
						0.87f,
						Color.AntiqueWhite);
					// Ingredient name
					DrawText(b, ingredientNameText, 1f, 48, textPosition.Y, null, true, drawColour);

					// Ingredient stock
					if (!Game1.options.showAdvancedCraftingInformation)
						continue;
					var position = new Point(_lineWidth - 64, (int)(textPosition.Y + 2));
					b.Draw(
						Game1.mouseCursors,
						new Rectangle(_leftContent.X + position.X, position.Y, 22, 26),
						new Rectangle(268, 1436, 11, 13),
						Color.White);
					DrawText(b, string.Concat(bagCount), 1f, position.X + 32, position.Y, 64, true, drawColour);
				}
			}
			else
			{
				textPosition.Y += 64 / 2 + 4;
				text = i18n.Get("menu.cooking_recipe.title_unknown");
				DrawText(b, text, 1f, 40, textPosition.Y, textWidth, true, SubtextColour);
			}

			// Recipe cooking duration and clock icon
			text = i18n.Get("menu.cooking_recipe.time_label");
			textPosition.Y = _cookbookLeftRect.Y + _cookbookLeftRect.Height - 56 - Game1.smallFont.MeasureString(
				Game1.parseText(text, Game1.smallFont, textWidth)).Y;
			DrawHorizontalDivider(b, 0, textPosition.Y, _lineWidth, true);
			textPosition.Y += TextDividerGap;
			DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, true);
			text = _currentRecipe.timesCrafted > 0
				? i18n.Get("menu.cooking_recipe.time_value", new { duration })
				: i18n.Get("menu.cooking_recipe.title_unknown");
			textPosition.X = _lineWidth - 16 - Game1.smallFont.MeasureString(
				Game1.parseText(text, Game1.smallFont, textWidth)).X;
			Utility.drawWithShadow(b,
				Game1.mouseCursors,
				new Vector2(_leftContent.X + textPosition.X, textPosition.Y + 6),
				new Rectangle(434, 475, 9, 9),
				Color.White, 0f, Vector2.Zero, 2f, false, 1f,
				-2, 2);
			textPosition.X += 24;
			DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, true);

			///////////// RIGHT PAGE /////////////

			// Cooking slots
			foreach (var clickable in CookingSlots)
				clickable.draw(b);

			for (var i = 0; i < _cookingSlotsDropIn.Count; ++i)
				_cookingSlotsDropIn[i]?.drawInMenu(b,
					new Vector2(
						CookingSlots[i].bounds.Location.X + CookingSlots[i].bounds.Width / 2 - 64 / 2,
						CookingSlots[i].bounds.Location.Y + CookingSlots[i].bounds.Height / 2 - 64 / 2),
					1f, 1f, 1f,
					StackDrawType.Draw, Color.White, true);

			textPosition.X = 0;
			textPosition.Y = 0;

			// Recipe notes
			text = i18n.Get("menu.cooking_recipe.notes_label");
			textPosition.Y = _cookbookRightRect.Y + _cookbookRightRect.Height - 196 - Game1.smallFont.MeasureString(
				Game1.parseText(text, Game1.smallFont, textWidth)).Y;
			DrawHorizontalDivider(b, 0, textPosition.Y, _lineWidth, false);
			textPosition.Y += TextDividerGap;
			DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, false, SubtextColour);
			textPosition.Y += Game1.smallFont.MeasureString(Game1.parseText(text, Game1.smallFont, textWidth)).Y;
			DrawHorizontalDivider(b, 0, textPosition.Y, _lineWidth, false);
			textPosition.Y += TextDividerGap * 2;
			if (_showCookingConfirmationPopup)
			{
				textPosition.Y += 16;
				textPosition.X += 64;

				// Contextual cooking popup
				var bounds = new Rectangle(
					_rightContent.X + (int)textPosition.X, (int)textPosition.Y,
					90, 90);
				Game1.DrawBox(bounds.X, bounds.Y, bounds.Width, bounds.Height);
				QuantityTextBox.Draw(b);
				CookConfirmButton.draw(b);
				CookCancelButton.draw(b);
				CookQuantityUpButton.draw(b);
				CookQuantityDownButton.draw(b);
				/*
				b.Draw(CookQuantityUpButton.texture,
					CookQuantityUpButton.bounds,
					CookQuantityUpButton.sourceRect,
					Color.White,
					(float)(270 * Math.PI / 180),
					new Vector2(CookQuantityUpButton.bounds.Width / 2f),
					SpriteEffects.None,
					1f);
				b.Draw(CookQuantityDownButton.texture,
					CookQuantityDownButton.bounds,
					CookQuantityDownButton.sourceRect,
					Color.White,
					(float)(270 * Math.PI / 180),
					new Vector2(CookQuantityDownButton.bounds.Width / 2f),
					SpriteEffects.None,
					1f);
				*/
			}
			else if (_cookingSlotsDropIn.Any(item => item != null))
			{
				textPosition.Y += 16;
				textPosition.X = _rightContent.X + _cookbookRightRect.Width / 2 - MarginRight;

				// Cook! button
				var source = CookButtonSource;
				source.X += _animFrame * CookButtonSource.Width;
				var dest = new Rectangle(
					(int)textPosition.X, (int)textPosition.Y,
					source.Width * Scale, source.Height * Scale);
				dest.X -= (CookTextSourceWidths[_locale] / 2 * Scale - CookTextSideWidth * Scale) + MarginLeft;
				var clickableArea = new Rectangle(dest.X, dest.Y, CookTextSideWidth * Scale * 2 + CookTextMiddleWidth * Scale, dest.Height);
				if (clickableArea.Contains(Game1.getMouseX(), Game1.getMouseY()))
					source.Y += source.Height;
				// left
				source.Width = CookTextSideWidth;
				dest.Width = source.Width * Scale;
				b.Draw(
					Texture, dest, source,
					Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);
				// middle and text
				source.X = _animFrame * CookButtonSource.Width + CookButtonSource.X + CookTextSideWidth;
				source.Width = 1;
				dest.Width = CookTextMiddleWidth * Scale;
				dest.X += CookTextSideWidth * Scale;
				b.Draw(
					Texture, dest, source,
					Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);
				b.Draw(
					Texture,
					new Rectangle(
						dest.X, dest.Y + (dest.Height - CookTextSource[_locale].Height * Scale) / 2
									   + _animTextOffsetPerFrame[_animFrame] * Scale,
						CookTextSource[_locale].Width * Scale, CookTextSource[_locale].Height * Scale),
					CookTextSource[_locale],
					Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);
				// right
				source.X = _animFrame * CookButtonSource.Width + CookButtonSource.X + CookButtonSource.Width - CookTextSideWidth;
				source.Width = CookTextSideWidth;
				dest.Width = source.Width * Scale;
				dest.X += CookTextMiddleWidth * Scale;
				b.Draw(
					Texture, dest, source,
					Color.White, 0f, Vector2.Zero, SpriteEffects.None, 1f);

				// DANCING FORKS
				/*var flipped = _animFrame >= 4 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
				for (var i = 0; i < 2; ++i)
				{
					var xSourceOffset = i == 1 ? 32 : 0;
					var ySourceOffset = _animFrame % 2 == 0 ? 32 : 0;
					var xDestOffset = i == 1 ? sideWidth * 2 * Scale + middleWidth * Scale + 96 : 0;
					b.Draw(
						Texture,
						new Vector2(_rightContent.X + xDestOffset - 8, dest.Y - 32),
						new Rectangle(128 + xSourceOffset, 16 + ySourceOffset, 32, 32),
						Color.White, 0f, Vector2.Zero, Scale, flipped, 1f);
				}*/
			}
			else if (!ModEntry.Instance.SaveData.FoodsEaten.ContainsKey(_recipeItem.Name)
				|| ModEntry.Instance.SaveData.FoodsEaten[_recipeItem.Name] < 1)
			{
				text = i18n.Get("menu.cooking_recipe.notes_unknown");
				DrawText(b, text, 1f, textPosition.X, textPosition.Y, textWidth, false, SubtextColour);
			}
			else
			{
				// Energy
				textPosition.X = -8f;
				text = Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3116",
					_recipeItem.staminaRecoveredOnConsumption());
				Utility.drawWithShadow(b,
					Game1.mouseCursors,
					new Vector2(_rightContent.X + textPosition.X, textPosition.Y),
					new Rectangle(0, 428, 10, 10),
					Color.White, 0f, Vector2.Zero, 3f);
				textPosition.X += 34f;
				DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, false, Game1.textColor);
				textPosition.Y += Game1.smallFont.MeasureString(Game1.parseText(text, Game1.smallFont, textWidth)).Y;
				// Health
				text = Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3118",
					_recipeItem.healthRecoveredOnConsumption());
				textPosition.X -= 34f;
				Utility.drawWithShadow(b,
					Game1.mouseCursors,
					new Vector2(_rightContent.X + textPosition.X, textPosition.Y),
					new Rectangle(0, 428 + 10, 10, 10),
					Color.White, 0f, Vector2.Zero, 3f);
				textPosition.X += 34f;
				DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, false, Game1.textColor);
				textPosition.Y -= Game1.smallFont.MeasureString(Game1.parseText(text, Game1.smallFont, textWidth)).Y;
				textPosition.X += -34f + _lineWidth / 2f + 16f;

				// Buffs
				for (var i = 0; i < Math.Min(4, _recipeBuffs.Count); ++i)
				{
					if (_recipeBuffs[i] == 0)
						continue;

					Utility.drawWithShadow(b,
						Game1.mouseCursors,
						new Vector2(_rightContent.X + textPosition.X, textPosition.Y),
						new Rectangle(10 + 10 * i, 428, 10, 10),
						Color.White, 0f, Vector2.Zero, 3f);
					textPosition.X += 34f;
					text = (_recipeBuffs[i] > 0 ? "+" : "")
						   + _recipeBuffs[i]
						   + Game1.content.LoadString($"Strings\\StringsFromCSFiles:Buff.cs.{480 + i * 3}");
					DrawText(b, text, 1f, textPosition.X, textPosition.Y, null, false, Game1.textColor);
					textPosition.Y += Game1.smallFont.MeasureString(Game1.parseText(text, Game1.smallFont, textWidth)).Y;
					textPosition.X -= 34f;
				}
			}

			///////////// OTHER BITS /////////////

			DrawInventoryMenu(b);
			DrawActualInventory(b);
			DrawExtraStuff(b);
		}

		/// <summary>
		/// Mostly a copy of InventoryMenu.draw(SpriteBatch b, int red, int blue, int green),
		/// though items considered unable to be cooked will be greyed out.
		/// </summary>
		private void DrawActualInventory(SpriteBatch b)
		{
			var iconShakeTimer = _iconShakeTimerField.GetValue();
			for (var key = 0; key < inventory.inventory.Count; ++key)
			{
				if (iconShakeTimer.ContainsKey(key)
				    && Game1.currentGameTime.TotalGameTime.TotalSeconds >= iconShakeTimer[key])
					iconShakeTimer.Remove(key);
			}
			for (var i = 0; i < inventory.capacity; ++i)
			{
				var position = new Vector2(
					inventory.xPositionOnScreen
					 + i % (inventory.capacity / inventory.rows) * 64
					 + inventory.horizontalGap * (i % (inventory.capacity / inventory.rows)),
					inventory.yPositionOnScreen
						+ i / (inventory.capacity / inventory.rows) * (64 + inventory.verticalGap)
						+ (i / (inventory.capacity / inventory.rows) - 1) * 4
						- (i >= inventory.capacity / inventory.rows
						   || !inventory.playerInventory || inventory.verticalGap != 0 ? 0 : 12));

				b.Draw(
					Game1.menuTexture,
					position,
					Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 10),
					Color.White, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.5f);

				if ((inventory.playerInventory || inventory.showGrayedOutSlots) && i >= Game1.player.maxItems.Value)
					b.Draw(
						Game1.menuTexture,
						position,
						Game1.getSourceRectForStandardTileSheet(Game1.menuTexture, 57),
						Color.White * 0.5f, 0.0f, Vector2.Zero, 1f, SpriteEffects.None, 0.5f);

				if (i >= 12 || !inventory.playerInventory)
					continue;

				var text = "";
				switch (i)
				{
					case 9:
						text = "0";
						break;
					case 10:
						text = "-";
						break;
					case 11:
						text = "=";
						break;
					default:
						text = string.Concat(i + 1);
						break;
				}
				var vector2 = Game1.tinyFont.MeasureString(text);
				b.DrawString(
					Game1.tinyFont,
					text,
					position + new Vector2((float)(32.0 - vector2.X / 2.0), -vector2.Y),
					i == Game1.player.CurrentToolIndex ? Color.Red : Color.DimGray);
			}
			for (var i = 0; i < inventory.capacity; ++i)
			{
				var colour = CanItemBeCooked(inventory.actualInventory[i]) ? Color.White : Color.DarkGray;

				var location = new Vector2(
					inventory.xPositionOnScreen
					 + i % (inventory.capacity / inventory.rows) * 64
					 + inventory.horizontalGap * (i % (inventory.capacity / inventory.rows)),
					inventory.yPositionOnScreen
						+ i / (inventory.capacity / inventory.rows) * (64 + inventory.verticalGap)
						+ (i / (inventory.capacity / inventory.rows) - 1) * 4
						- (i >= inventory.capacity / inventory.rows
						   || !inventory.playerInventory || inventory.verticalGap != 0 ? 0 : 12));

				if (inventory.actualInventory.Count <= i || inventory.actualInventory.ElementAt(i) == null)
					continue;

				var drawShadow = inventory.highlightMethod(inventory.actualInventory[i]);
				if (iconShakeTimer.ContainsKey(i))
					location += 1f * new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2));
				inventory.actualInventory[i].drawInMenu(
					b,
					location,
					inventory.inventory.Count > i ? inventory.inventory[i].scale : 1f,
					!inventory.highlightMethod(inventory.actualInventory[i]) ? 0.25f : 1f,
					0.865f,
					StackDrawType.Draw,
					colour,
					drawShadow);
			}
		}

		private void DrawInventoryMenu(SpriteBatch b)
		{
			// Card
			Game1.drawDialogueBox(
				inventory.xPositionOnScreen - borderWidth / 2 - 32,
				inventory.yPositionOnScreen - borderWidth - spaceToClearTopBorder + 28,
				width,
				height - (borderWidth + spaceToClearTopBorder + 192) - 12,
				false, true);

			// Items
			inventory.draw(b);

			var iconShakeTimer = _iconShakeTimerField.GetValue();
			for (var key = 0; key < inventory.inventory.Count; ++key)
				if (iconShakeTimer.ContainsKey(key)
				    && Game1.currentGameTime.TotalGameTime.TotalSeconds >= iconShakeTimer[key])
					iconShakeTimer.Remove(key);
		}

		private void DrawExtraStuff(SpriteBatch b)
		{
			/*
			if (message != null)
			{
				Game1.drawDialogueBox(
					Game1.viewport.Width / 2, ItemsToGrabMenu.yPositionOnScreen + ItemsToGrabMenu.height / 2,
					false, false, message);
			}
			if (poof != null)
			{
				poof.draw(b, true);
			}
			*/

			foreach (var transferredItemSprite in _transferredItemSprites)
				transferredItemSprite.Draw(b);

			specialButton?.draw(b);
			upperRightCloseButton.draw(b);

			// Hover text
			if (hoverText != null && (hoveredItem == null || ItemsToGrabMenu == null))
			{
				if (hoverAmount > 0)
					drawToolTip(b, hoverText, "", null, true, -1, 0,
						-1, -1, null, hoverAmount);
				else
					drawHoverText(b, hoverText, Game1.smallFont);
			}

			// Trashcan
			if (trashCan != null)
			{
				trashCan.draw(b);
				b.Draw(Game1.mouseCursors,
					new Vector2(trashCan.bounds.X + 60, trashCan.bounds.Y + 40),
					new Rectangle(564 + Game1.player.trashCanLevel * 18, 129, 18, 10),
					Color.White, trashCanLidRotation, new Vector2(16f, 10f), Scale, SpriteEffects.None, 0.86f);
			}

			// Hover elements
			if (hoveredItem != null)
				drawToolTip(b, hoveredItem.getDescription(), hoveredItem.DisplayName, hoveredItem, heldItem != null);
			else if (hoveredItem != null && ItemsToGrabMenu != null)
				drawToolTip(b, ItemsToGrabMenu.descriptionText, ItemsToGrabMenu.descriptionTitle, hoveredItem, heldItem != null);
			heldItem?.drawInMenu(b, new Vector2(Game1.getOldMouseX() + 8, Game1.getOldMouseY() + 8), 1f);

			// Cursor
			Game1.mouseCursorTransparency = 1f;
			drawMouse(b);
		}

		private void DrawText(SpriteBatch b, string text, float scale, float x, float y, float? w, bool isLeftSide, Color? colour = null)
		{
			var position = isLeftSide ? _leftContent : _rightContent;
			position.Y -= yPositionOnScreen;
			Utility.drawTextWithShadow(b, Game1.parseText(text, Game1.smallFont,
				w != null ? (int)w : (int)Game1.smallFont.MeasureString(text).X), Game1.smallFont,
				new Vector2(position.X + x, position.Y + y), colour ?? Game1.textColor, scale);
		}

		private void DrawHorizontalDivider(SpriteBatch b, float x, float y, int w, bool isLeftSide)
		{
			var position = isLeftSide ? _leftContent : _rightContent;
			position.Y -= yPositionOnScreen;
			Utility.drawLineWithScreenCoordinates(
				position.X + TextMuffinTopOverDivider, (int)(position.Y + y),
				position.X + w + TextMuffinTopOverDivider, (int)(position.Y + y),
				b, DividerColour);
		}
	}
}