/*
	Copyright 2011 ForgeCraft team
	
	Dual-licensed under the	Educational Community License, Version 2.0 and
	the GNU General Public License, Version 3 (the "Licenses"); you may
	not use this file except in compliance with the Licenses. You may
	obtain a copy of the Licenses at
	
	http://www.opensource.org/licenses/ecl2.php
	http://www.gnu.org/licenses/gpl-3.0.html
	
	Unless required by applicable law or agreed to in writing,
	software distributed under the Licenses are distributed on an "AS IS"
	BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
	or implied. See the Licenses for the specific language governing
	permissions and limitations under the Licenses.
*/
using System;
using System.Collections.Generic;

namespace SMP
{
	public class Windows
	{
		public byte type; //This holds type information, used in deciding which kind of window we need to send.
		public string name = "Chest";
		public Point3 pos; //The pos of the block that this window is attached to
		public World level;
		public Item[] items; //Hold all the items this window has inside it.

		public Windows(byte Type, Point3 Pos, World Level)
		{
			Server.Log("Window Creating.");

			type = Type;
			pos = Pos;
			level = Level;

			switch (type)
			{
				case 0:
					name = "Chest"; //We change this to "Large Chest" Later if it needs it :3
					items = new Item[27];
					break;
				case 1:
					name = "Workbench";
					items = new Item[10];
					break;
				case 2:
					name = "Furnace";
					items = new Item[3];
					break;
				case 3:
					name = "Dispenser";
					items = new Item[9];
					break;
				default:
					name = "Chest";
					items = new Item[27];
					break;
			}
			Server.Log("Window adding.");
			level.windows.Add(pos, this);
			Server.Log("Window done.");
		}

		//public bool AddItem(Item item)
		//{
		//    byte slot = FindEmptySlot();
		//    if (slot == 255) return false;

		//    return AddItem(item, slot);
		//}
		public bool AddItem(Item item, byte slot)
		{
			return false;
		}

		public void Remove(Player p, int slot)
		{
			if (slot < items.Length)
			{
				items[slot] = Item.Nothing;
			}
			else
				p.inventory.Remove((slot - items.Length) + 9);
				
		}
		public void Remove(Player p, int slot, byte count)
		{
			if (slot < items.Length)
			{
				items[slot].count -= count;
			}
			else
				p.inventory.Remove((slot - items.Length) + 9, count);

		}

		public Item Right_Click(Player p, int slot)
		{
			if (slot > items.Length)
			{
				return p.inventory.Right_Click((slot - items.Length) + 9);
			}
			else
			{
				try
				{
					Item temp = new Item(items[slot].item, 0, items[slot].meta, p.level);
					if (items[slot].count == 1)
					{
						temp = items[slot];
						items[slot] = Item.Nothing;
						return temp;
					}
					if (items[slot].count % 2 == 0)
					{
						temp.count = (byte)(items[slot].count / 2);
						items[slot].count = (byte)(items[slot].count / 2);
					}
					else
					{
						byte a = items[slot].count;
						items[slot].count = (byte)(a / 2);
						temp.count = (byte)(a - items[slot].count);
					}
					return temp;
				}
				catch
				{
					return Item.Nothing;
				}
			}
		}
		
		public void HandleClick(Player p, byte[] message)
		{
			byte id = message[0];
			short slot = util.EndianBitConverter.Big.ToInt16(message, 1);
			ClickType click = (ClickType)message[3];
			short ActionID = util.EndianBitConverter.Big.ToInt16(message, 4);
			bool Shift = (message[6] == 1);
			short ItemID = util.EndianBitConverter.Big.ToInt16(message, 7);
			byte Count = 1;
			short Meta = 0;
			if (ItemID != -1)
			{
				Count = message[9];
				Meta = util.EndianBitConverter.Big.ToInt16(message, 10);
			}

			if (slot == -999)
			{
				//TODO throw item
				p.OnMouse = Item.Nothing;
				return;
			}

			if (slot > items.Length && !Shift)
			{
				p.inventory.ClickHandler((short)((slot - items.Length) + 9), click, ActionID, Shift, ItemID, Count, Meta);
				return;
			}

			if (Shift)
			{
				if (type == 3) return;
				else if (type == 0)
				{
					if (slot > items.Length)
					{
						//TODO Inventory to Chest
					}
					else
					{
						//TODO Chest to Inventory
					}
				}
				else if (type == 1)
				{
					if (slot > items.Length)
					{
						p.inventory.ClickHandler((short)((slot - items.Length) + 9), click, ActionID, Shift, ItemID, Count, Meta);
						return;
					}
					else
					{
						//TODO Workbench to Inventory
					}
				}
				else if (type == 2)
				{
					if (slot > items.Length)
					{
						p.inventory.ClickHandler((short)((slot - items.Length) + 9), click, ActionID, Shift, ItemID, Count, Meta);
						return;
					}
					else
					{
						//TODO Furnace to Inventory
					}
				}
			}
			else
			{
				if (p.OnMouse != Item.Nothing)
				{
					if (items[slot] != Item.Nothing)
					{
						#region Crafting/Furnace Output
						if ((type == 1 && slot == 0) || (type == 2 && slot == 0))
						{
							if (p.OnMouse.item == items[slot].item)
							{
								if (p.OnMouse.item < 255)
								{
									if (p.OnMouse.meta == items[slot].meta)
									{
										byte stacking = Inventory.isStackable(p.OnMouse.item);
										byte availible = (byte)(stacking - p.OnMouse.count);
										if (items[slot].count <= availible)
										{
											p.OnMouse.count += items[slot].count;
										}
									}
									else
									{
										Item temp = items[slot];
										items[slot] = p.OnMouse;
										p.OnMouse = temp;
									}
								}
								else
								{
									byte stacking = Inventory.isStackable(p.OnMouse.item);
									byte availible = (byte)(stacking - p.OnMouse.count);
									if (items[slot].count <= availible)
									{
										p.OnMouse.count += items[slot].count;
									}
								}
							}
							else
							{
								return;
							}
						}
						#endregion
						else
						{
							if (click == ClickType.RightClick)
							{
								if (p.OnMouse.item == items[slot].item)
								{
									if (p.OnMouse.item < 255)
									{
										if (p.OnMouse.meta == items[slot].meta)
										{
											byte stacking = Inventory.isStackable(p.OnMouse.item);
											if (items[slot].count < stacking)
											{
												items[slot].count += 1;
												if (p.OnMouse.count == 1)
												{
													p.OnMouse = Item.Nothing;
												}
												else
												{
													p.OnMouse.count -= 1;
												}
											}
										}
										else
										{
											Item temp = items[slot];
											items[slot] = p.OnMouse;
											p.OnMouse = temp;
										}
									}
									else
									{
										byte stacking = Inventory.isStackable(p.OnMouse.item);
										if (items[slot].count < stacking)
										{
											items[slot].count += 1;
											if (p.OnMouse.count == 1)
											{
												p.OnMouse = Item.Nothing;
											}
											else
											{
												p.OnMouse.count -= 1;
											}
										}
									}
								}
							}
							else
							{
								if (p.OnMouse.item == items[slot].item)
								{
									if (p.OnMouse.item < 255)
									{
										if (p.OnMouse.meta == items[slot].meta)
										{
											byte stacking = Inventory.isStackable(p.OnMouse.item);
											byte available = (byte)(stacking - items[slot].count);
											if (available == 0) return;
											if (p.OnMouse.count <= available)
											{
												items[slot].count += p.OnMouse.count;
												p.OnMouse = Item.Nothing;
											}
											else
											{
												items[slot].count = stacking;
												p.OnMouse.count -= available;
											}
										}
										else
										{
											Item temp = items[slot];
											items[slot] = p.OnMouse;
											p.OnMouse = temp;
										}
									}
									else
									{
										byte stacking = Inventory.isStackable(p.OnMouse.item);
										byte available = (byte)(stacking - items[slot].count);
										if (available == 0) return;
										if (p.OnMouse.count <= available)
										{
											items[slot].count += p.OnMouse.count;
											p.OnMouse = Item.Nothing;
										}
										else
										{
											items[slot].count = stacking;
											p.OnMouse.count -= available;
										}
									}
								}
								else
								{
									Item temp = items[slot];
									items[slot] = p.OnMouse;
									p.OnMouse = temp;
								}
							}
						}
					}
					#region Empty Slot Done
					else
					{
						if (slot == 0) return; //Crafting output slot
						#region Armor slots Done
						if (slot == 5 || slot == 6 || slot == 7 || slot == 8)
						{
							switch (slot)
							{
								case 5:
									if (p.OnMouse.item == 298 || p.OnMouse.item == 302 || p.OnMouse.item == 306 || p.OnMouse.item == 310)
									{
										items[slot] = p.OnMouse;
										p.OnMouse = Item.Nothing;
									}
									break;
								case 6:
									if (p.OnMouse.item == 299 || p.OnMouse.item == 303 || p.OnMouse.item == 307 || p.OnMouse.item == 311)
									{
										items[slot] = p.OnMouse;
										p.OnMouse = Item.Nothing;
									}
									break;
								case 7:
									if (p.OnMouse.item == 300 || p.OnMouse.item == 304 || p.OnMouse.item == 308 || p.OnMouse.item == 312)
									{
										items[slot] = p.OnMouse;
										p.OnMouse = Item.Nothing;
									}
									break;
								case 8:
									if (p.OnMouse.item == 301 || p.OnMouse.item == 305 || p.OnMouse.item == 309 || p.OnMouse.item == 313)
									{
										items[slot] = p.OnMouse;
										p.OnMouse = Item.Nothing;
									}
									break;
							}
						}
						#endregion
						else
						{
							if (click == ClickType.RightClick)
							{
								if (p.OnMouse.count == 1)
								{
									items[slot] = p.OnMouse;
									p.OnMouse = Item.Nothing;
								}
								else
								{
									p.OnMouse.count -= 1;
									items[slot] = new Item(p.OnMouse.item, 1, p.OnMouse.meta, p.level);
								}
							}
							else
							{
								items[slot] = p.OnMouse;
								p.OnMouse = Item.Nothing;
							}
						}
					}
					#endregion
				}
				#region Empty Mouse done
				else //Player has NOTHING on the mouse
				{
					if (items[slot] != Item.Nothing)
					{
						if (click == ClickType.RightClick)
						{
							p.OnMouse = Right_Click(p, slot);
						}
						else //Player left-clicked
						{
							p.OnMouse = items[slot];
							Remove(p, slot);
						}
					}
					else
					{
						return;
					}
				}
				#endregion
			}
		}

		public byte GetEmptyChestSlot()
		{
			for (byte i = 0; i < items.Length; i++)
				if (items[i] == Item.Nothing)
					return i;
			return 255;
		}

		public byte GetEmptyHotbarSlot(Player p)
		{
			for (byte i = 36; i < 45; i++)
				if (p.inventory.items[i].item == (short)Items.Nothing)
					return i;
			return 255;
		}
		public byte GetEmptyHotbarSlotReversed(Player p)
		{
			for (byte i = 44; i >= 36; i--)
				if (p.inventory.items[i].item == (short)Items.Nothing)
					return i;
			return 255;
		}

		public byte GetEmptyInventorySlot(Player p)
		{
			for (byte i = 9; i <= 35; i++)
				if (p.inventory.items[i].item == (short)Items.Nothing)
					return i;
			return 255;
		}
		public byte GetEmptyInvetorySlotReversed(Player p)
		{
			for (byte i = 35; i >= 9; i--)
				if (items[i].item == (short)Items.Nothing)
					return i;
			return 255;
		}
	}
}

