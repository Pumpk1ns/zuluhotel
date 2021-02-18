using System;
using System.Linq;
using Server.Items;
using Server.Network;
using ZuluContent.Zulu.Engines.Magic;
using ZuluContent.Zulu.Engines.Magic.Enchantments;
using static Server.Configurations.MessageHueConfiguration;
using static Server.Configurations.ResourceConfiguration;

namespace Server.Engines.Harvest
{
    public class Lumberjacking : HarvestSystem
    {
        private static Lumberjacking m_System;

        public static Lumberjacking System
        {
            get
            {
                if (m_System == null)
                    m_System = new Lumberjacking();

                return m_System;
            }
        }

        public HarvestDefinition Definition { get; set; }

        private Lumberjacking()
        {
            var res = LogConfiguration.Entries.Where(e => e.HarvestSkillRequired > 0.0).Select(e =>
                new HarvestResource(e.HarvestSkillRequired, e.Name, e.ResourceType)).ToArray();
            var veins = LogConfiguration.Entries.Where(e => e.VeinChance > 0.0).Select((e, i) =>
                new HarvestVein(e.VeinChance, res[i])).OrderBy(v => v.VeinChance).ToArray();

            #region Lumberjacking

            HarvestDefinition lumber = new HarvestDefinition();

            // Resource banks are every 1x1 tiles
            lumber.BankWidth = 1;
            lumber.BankHeight = 1;

            // Every bank holds from 45 to 90 logs
            lumber.MinTotal = 45;
            lumber.MaxTotal = 90;

            // A resource bank will respawn its content every 20 to 30 minutes
            lumber.MinRespawn = TimeSpan.FromMinutes(20.0);
            lumber.MaxRespawn = TimeSpan.FromMinutes(30.0);

            // Skill checking is done on the Lumberjacking skill
            lumber.Skill = SkillName.Lumberjacking;

            // Set the list of harvestable tiles
            lumber.Tiles = m_TreeTiles;

            // Players must be within 2 tiles to harvest
            lumber.MaxRange = 2;

            // Ten logs per harvest action
            lumber.ConsumedPerHarvest = skillValue => (int) (skillValue / 15) + 1;

            // Maximum chance to roll for colored veins
            lumber.MaxChance = 100;

            // The chopping effect
            lumber.EffectActions = new[] {13};
            lumber.EffectSounds = new[] {0x13E};
            lumber.EffectCounts = new[] {4};
            lumber.EffectDelay = TimeSpan.FromSeconds(1.6);
            lumber.EffectSoundDelay = TimeSpan.FromSeconds(0.9);

            lumber.NoResourcesMessage = 500493; // There's not enough wood here to harvest.
            lumber.FailMessage = 500495; // You hack at the tree for a while, but fail to produce any useable wood.
            lumber.OutOfRangeMessage = 500446; // That is too far away.
            lumber.PackFullMessage = 500497; // You can't place any wood into your backpack!
            lumber.ToolBrokeMessage = 500499; // You broke your axe.

            lumber.Resources = res;
            lumber.Veins = veins;

            lumber.DefaultVein = new HarvestVein(0.0, new HarvestResource(0.0,
                500498,
                typeof(Log)));

            lumber.BonusEffect = HarvestBonusEffect;

            Definition = lumber;
            Definitions.Add(lumber);

            #endregion
        }

        private static void HarvestBonusEffect(Mobile harvester, Item tool)
        {
            var chance = Utility.Random(1, 100);
            var bonus = (int) (harvester.Skills[SkillName.Lumberjacking].Value / 30);

            harvester.FireHook(h => h.OnHarvestBonus(harvester, ref bonus));

            chance += bonus;

            if (tool is IEnchanted enchantedTool && enchantedTool.Enchantments.Get((HarvestBonus e) => e.Value) > 0)
            {
                var toolBonusChance = 2;
                harvester.FireHook(h => h.OnToolHarvestBonus(harvester, ref toolBonusChance));
                chance += toolBonusChance;
            }

            Item item = null;
            var message = "";

            switch (chance)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                {
                    message = "Oh no your tool breaks!";
                    tool.Delete();
                    break;
                }
                case 96:
                case 97:
                case 98:
                case 99:
                case 100:
                {
                    item = new DeadWood(1);
                    message = "You find some dead wood!";
                    break;
                }
                case 101:
                case 102:
                case 103:
                case 104:
                {
                    item = new YoungOakLog(1);
                    message = "You find some young oak wood!";
                    break;
                }
                case 105:
                case 106:
                case 107:
                case 108:
                {
                    item = new DeadWood(2);
                    message = "You find some dead wood!";
                    break;
                }
                case 109:
                case 110:
                {
                    item = new YoungOakLog(2);
                    message = "You find some young oak wood!";
                    break;
                }
                case 111:
                case 112:
                {
                    item = new DeadWood(3);
                    message = "You find some dead wood!";
                    break;
                }
                case 113:
                case 114:
                {
                    item = new YoungOakLog(3);
                    message = "You find some young oak wood!";
                    break;
                }
                case 115:
                {
                    item = new YoungOakLog(4);
                    message = "You find some young oak wood!";
                    break;
                }
            }

            if (item != null)
            {
                var cont = harvester.Backpack;
                if (cont.TryDropItem(harvester, item, false))
                {
                    if (message.Length > 0)
                        harvester.SendAsciiMessage(MessageSuccessHue, message);
                }
                else if (message.Length > 0)
                    harvester.SendAsciiMessage(MessageFailureHue, message);
            }
        }

        public override bool CheckHarvest(Mobile from, Item tool)
        {
            if (!base.CheckHarvest(from, tool))
                return false;

            if (tool.Parent != from)
            {
                from.SendLocalizedMessage(500487); // The axe must be equipped for any serious wood chopping.
                return false;
            }

            return true;
        }

        public override bool CheckHarvest(Mobile from, Item tool, HarvestDefinition def, object toHarvest)
        {
            if (!base.CheckHarvest(from, tool, def, toHarvest))
                return false;

            if (tool.Parent != from)
            {
                from.SendLocalizedMessage(500487); // The axe must be equipped for any serious wood chopping.
                return false;
            }

            return true;
        }

        public override void OnHarvestStarted(Mobile from, Item tool, HarvestDefinition def, object toHarvest)
        {
            from.SendAsciiMessage(MessageSuccessHue, "You start lumberjacking...");
        }

        public override void OnBadHarvestTarget(Mobile from, Item tool, object toHarvest)
        {
            if (toHarvest is Mobile)
                ((Mobile) toHarvest).PrivateOverheadMessage(MessageType.Regular, 0x3B2, 500450,
                    from.NetState); // You can only skin dead creatures.
            else if (toHarvest is Item)
                ((Item) toHarvest).LabelTo(from, 500464); // Use this on corpses to carve away meat and hide
            else if (toHarvest is Targeting.StaticTarget || toHarvest is Targeting.LandTarget)
                from.SendLocalizedMessage(500489); // You can't use an axe on that.
            else
                from.SendLocalizedMessage(1005213); // You can't do that
        }

        public static void Initialize()
        {
            Array.Sort(m_TreeTiles);
        }

        #region Tile lists

        private static int[] m_TreeTiles = new[]
        {
            0x4CCA, 0x4CCB, 0x4CCC, 0x4CCD, 0x4CD0, 0x4CD3, 0x4CD6, 0x4CD8,
            0x4CDA, 0x4CDD, 0x4CE0, 0x4CE3, 0x4CE6, 0x4CF8, 0x4CFB, 0x4CFE,
            0x4D01, 0x4D41, 0x4D42, 0x4D43, 0x4D44, 0x4D57, 0x4D58, 0x4D59,
            0x4D5A, 0x4D5B, 0x4D6E, 0x4D6F, 0x4D70, 0x4D71, 0x4D72, 0x4D84,
            0x4D85, 0x4D86, 0x52B5, 0x52B6, 0x52B7, 0x52B8, 0x52B9, 0x52BA,
            0x52BB, 0x52BC, 0x52BD,

            0x4CCE, 0x4CCF, 0x4CD1, 0x4CD2, 0x4CD4, 0x4CD5, 0x4CD7, 0x4CD9,
            0x4CDB, 0x4CDC, 0x4CDE, 0x4CDF, 0x4CE1, 0x4CE2, 0x4CE4, 0x4CE5,
            0x4CE7, 0x4CE8, 0x4CF9, 0x4CFA, 0x4CFC, 0x4CFD, 0x4CFF, 0x4D00,
            0x4D02, 0x4D03, 0x4D45, 0x4D46, 0x4D47, 0x4D48, 0x4D49, 0x4D4A,
            0x4D4B, 0x4D4C, 0x4D4D, 0x4D4E, 0x4D4F, 0x4D50, 0x4D51, 0x4D52,
            0x4D53, 0x4D5C, 0x4D5D, 0x4D5E, 0x4D5F, 0x4D60, 0x4D61, 0x4D62,
            0x4D63, 0x4D64, 0x4D65, 0x4D66, 0x4D67, 0x4D68, 0x4D69, 0x4D73,
            0x4D74, 0x4D75, 0x4D76, 0x4D77, 0x4D78, 0x4D79, 0x4D7A, 0x4D7B,
            0x4D7C, 0x4D7D, 0x4D7E, 0x4D7F, 0x4D87, 0x4D88, 0x4D89, 0x4D8A,
            0x4D8B, 0x4D8C, 0x4D8D, 0x4D8E, 0x4D8F, 0x4D90, 0x4D95, 0x4D96,
            0x4D97, 0x4D99, 0x4D9A, 0x4D9B, 0x4D9D, 0x4D9E, 0x4D9F, 0x4DA1,
            0x4DA2, 0x4DA3, 0x4DA5, 0x4DA6, 0x4DA7, 0x4DA9, 0x4DAA, 0x4DAB,
            0x52BE, 0x52BF, 0x52C0, 0x52C1, 0x52C2, 0x52C3, 0x52C4, 0x52C5,
            0x52C6, 0x52C7
        };

        #endregion
    }
}