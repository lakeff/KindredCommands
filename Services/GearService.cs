
using System.Linq;
using Il2CppInterop.Runtime;
using KindredCommands.Data;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace KindredCommands.Services;

internal class GearService
{
	EntityQuery itemQuery;

	readonly static PrefabGUID[] shardPrefabs = [
		Prefabs.Item_MagicSource_SoulShard_Dracula,
		Prefabs.Item_MagicSource_SoulShard_Manticore,
		Prefabs.Item_MagicSource_SoulShard_Monster,
		Prefabs.Item_MagicSource_SoulShard_Solarus,
		Prefabs.Item_MagicSource_SoulShard_Morgana
	];

	public GearService()
	{
		var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
			.AddAll(new(Il2CppType.Of<InventoryItem>(), ComponentType.AccessMode.ReadWrite))
			.AddAll(new(Il2CppType.Of<PrefabGUID>(), ComponentType.AccessMode.ReadWrite))
			.AddAll(new(Il2CppType.Of<ItemData>(), ComponentType.AccessMode.ReadWrite))
			.AddAll(new(Il2CppType.Of<EquippableData>(), ComponentType.AccessMode.ReadWrite))
			.WithOptions(EntityQueryOptions.IncludeDisabled | EntityQueryOptions.IncludePrefab);
		itemQuery = Core.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
		entityQueryBuilder.Dispose();

		SetHeadgearBloodbound(Core.ConfigSettings.HeadgearBloodbound);
	}

	public bool ToggleHeadgearBloodbound()
	{
		Core.ConfigSettings.HeadgearBloodbound = !Core.ConfigSettings.HeadgearBloodbound;
		SetHeadgearBloodbound(Core.ConfigSettings.HeadgearBloodbound);
		return Core.ConfigSettings.HeadgearBloodbound;
	}

	void SetHeadgearBloodbound(bool bloodBound)
	{
		var itemMap = Core.GameDataSystem.ItemHashLookupMap;
		var allHeadgear = Helper.GetEntitiesByComponentTypes<EquipmentToggleData, Prefab>(includePrefab:true);
		foreach (var headgear in allHeadgear)
		{
			var equipData = headgear.Read<EquippableData>();
			var itemData = headgear.Read<ItemData>();
			var prefabGUID = headgear.Read<PrefabGUID>();

			if (prefabGUID.GuidHash == -511360389)
			{ 
				itemData.ItemCategory |= ItemCategory.BloodBound;
				headgear.Write(itemData);
			}
			if (equipData.EquipmentType != EquipmentType.Headgear) continue;
			if(bloodBound)
				itemData.ItemCategory |= ItemCategory.BloodBound;
			else
				itemData.ItemCategory &= ~ItemCategory.BloodBound;
			headgear.Write(itemData);

			itemMap[prefabGUID] = itemData;
		}
	}
	public bool ToggleShardsFlightRestricted()
	{
		Core.ConfigSettings.SoulshardsFlightRestricted = !Core.ConfigSettings.SoulshardsFlightRestricted;
		return Core.ConfigSettings.SoulshardsFlightRestricted;
	}

	public void SetShardsRestricted(bool shardsRestricted)
	{

		var newCategory = shardsRestricted ? 
			ItemCategory.Soulshard | (Core.SoulshardService.IsPlentiful ? ItemCategory.BloodBound : ItemCategory.NONE) :
			ItemCategory.Magic;
		var itemMap = Core.GameDataSystem.ItemHashLookupMap;
		foreach (var prefabGUID in shardPrefabs)
		{
			if (!itemMap.TryGetValue(prefabGUID, out var itemData)) continue;
			
			itemData.ItemCategory = newCategory;
			itemMap[prefabGUID] = itemData;
		}

		var entities = itemQuery.ToEntityArray(Allocator.Temp);
		foreach (var entity in entities)
		{
			if (!shardPrefabs.Contains(entity.Read<PrefabGUID>())) continue;

			var itemData = entity.Read<ItemData>();
			itemData.ItemCategory = newCategory;
			entity.Write(itemData);
		}
		entities.Dispose();
	}
}
