using System;
using System.Text.Json;
using Server.Json;
using Server.Regions;

namespace Server.Engines.Spawners
{
  public class RegionSpawner : Spawner
  {
    private BaseRegion m_SpawnRegion;

    [CommandProperty(AccessLevel.Developer)]
    public BaseRegion SpawnRegion
    {
      get => m_SpawnRegion;
      set
      {
        m_SpawnRegion = value;
        m_SpawnRegion?.InitRectangles();

        InvalidateProperties();
      }
    }

    [Constructible(AccessLevel.Developer)]
    public RegionSpawner()
    {
    }

    [Constructible(AccessLevel.Developer)]
    public RegionSpawner(string spawnedName) : base(spawnedName)
    {
    }

    [Constructible(AccessLevel.Developer)]
    public RegionSpawner(int amount, int minDelay, int maxDelay, int team, int homeRange,
      params string[] spawnedNames) : this(amount, TimeSpan.FromMinutes(minDelay), TimeSpan.FromMinutes(maxDelay),
      team, homeRange, spawnedNames)
    {
    }

    [Constructible(AccessLevel.Developer)]
    public RegionSpawner(int amount, TimeSpan minDelay, TimeSpan maxDelay, int team, int homeRange,
      params string[] spawnedNames) : base(amount, minDelay, maxDelay, team, homeRange, spawnedNames)
    {
    }

    public RegionSpawner(DynamicJson json, JsonSerializerOptions options) : base(json, options)
    {
      json.GetProperty("map", options, out Map map);
      json.GetProperty("region", options, out string spawnRegion);

      m_SpawnRegion = Region.Find(spawnRegion, map) as BaseRegion;
      m_SpawnRegion?.InitRectangles();
    }

    public RegionSpawner(Serial serial) : base(serial)
    {
    }

    public override void GetSpawnerProperties(ObjectPropertyList list)
    {
      base.GetSpawnerProperties(list);

      if (Running && m_SpawnRegion != null) list.Add(1076228, "region:\t{0}", m_SpawnRegion.Name); // ~1_DUMMY~ ~2_DUMMY~
    }

    public override Point3D GetSpawnPosition(ISpawnable spawned, Map map)
    {
      if (m_SpawnRegion == null || map == null || map == Map.Internal || map != m_SpawnRegion.Map || m_SpawnRegion.TotalWeight <= 0)
        return Location;

      bool waterMob, waterOnlyMob;

      if (spawned is Mobile mob)
      {
        waterMob = mob.CanSwim;
        waterOnlyMob = mob.CanSwim && mob.CantWalk;
      }
      else
      {
        waterMob = false;
        waterOnlyMob = false;
      }

      // Try 10 times to find a valid location.
      for (int i = 0; i < 10; i++)
      {
        int rand = Utility.Random(m_SpawnRegion.TotalWeight);

        int x = int.MinValue;
        int y = int.MinValue;

        for (int j = 0; j < m_SpawnRegion.RectangleWeights.Length; j++)
        {
          int curWeight = m_SpawnRegion.RectangleWeights[j];

          if (rand < curWeight)
          {
            Rectangle3D rect = m_SpawnRegion.Rectangles[j];

            x = rect.Start.X + rand % rect.Width;
            y = rect.Start.Y + rand / rect.Width;

            break;
          }

          rand -= curWeight;
        }

        int mapZ = map.GetAverageZ(x, y);

        if (waterMob)
        {
          if (IsValidWater(map, x, y, Z))
            return new Point3D(x, y, Z);
          if (IsValidWater(map, x, y, mapZ))
            return new Point3D(x, y, mapZ);
        }

        if (!waterOnlyMob)
        {
          if (map.CanSpawnMobile(x, y, Z))
            return new Point3D(x, y, Z);
          if (map.CanSpawnMobile(x, y, mapZ))
            return new Point3D(x, y, mapZ);
        }
      }

      return HomeLocation;
    }

    public override void Deserialize(IGenericReader reader)
    {
      base.Deserialize(reader);

      int version = reader.ReadEncodedInt();

      m_SpawnRegion = Region.Find(reader.ReadString(), Map) as BaseRegion;
      m_SpawnRegion?.InitRectangles();
    }

    public override void Serialize(IGenericWriter writer)
    {
      base.Serialize(writer);

      writer.WriteEncodedInt(0); // version

      writer.Write(m_SpawnRegion?.Name);
    }
  }
}
