using KindredCommands.Models;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using UnityEngine;
using VampireCommandFramework;

namespace KindredCommands.Commands;

internal static class BloodCommands
{
	[Command("bp", description: "Cria uma poção com dois sangues. EX: .bp Brute 100 Warrior 100 1", adminOnly: false)]
	public static void GiveBloodMerlotCommand(ChatCommandContext ctx, BloodType primaryType = BloodType.Frailed, float primaryQuality = 100f, BloodType secondaryType = BloodType.Frailed, float secondaryQuality=100f, int secondaryTrait=1, int quantity = 1)
	{
		primaryQuality = Mathf.Clamp(primaryQuality, 100);
		secondaryQuality = Mathf.Clamp(secondaryQuality, 100);
		secondaryTrait = Mathf.Clamp(secondaryTrait, 1, 3);
		for (var i = 0; i < quantity; i++)
		{
			Entity entity = Helper.AddItemToInventory(ctx.Event.SenderCharacterEntity, new PrefabGUID(1223264867), 1);

			if (entity == Entity.Null)
			{
				ctx.Reply($"Criado <color=#ff0>{i}</color> poção de sangue <color=#ff0>{primaryType}</color> de <color=#ff0>{primaryQuality}</color>% qualidade "+
					      $"com <color=#ff0>{secondaryType}</color> de <color=#ff0>{secondaryQuality}</color>% qualidade e trait {secondaryTrait}");
				ctx.Reply($"Inventário está cheio, não foi possivel adicionar <color=#ff0>{quantity - i}</color> poção de sangue");
				return;
			}

			var blood = new StoredBlood()
			{
				BloodQuality = primaryQuality,
				PrimaryBloodType = new PrefabGUID((int)primaryType),
				SecondaryBlood = new()
				{
					Quality = secondaryQuality,
					Type = new PrefabGUID((int)secondaryType),
					BuffIndex = (byte)(secondaryTrait - 1)
				}
			};

			Core.EntityManager.SetComponentData(entity, blood);
		}

		ctx.Reply($"Criado <color=#ff0>{quantity}</color> poção de sangue <color=#ff0>{primaryType}</color> de <color=#ff0>{primaryQuality}</color>% qualidade " +
				  $"com <color=#ff0>{secondaryType}</color> de <color=#ff0>{secondaryQuality}</color>% qualidade e trait {secondaryTrait}");
	}
}
