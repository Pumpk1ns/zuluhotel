using System.Collections.Generic;

namespace Server.Mobiles
{
    public class Thief : BaseVendor
    {
        private List<SBInfo> m_SBInfos = new List<SBInfo>();

        protected override List<SBInfo> SBInfos
        {
            get { return m_SBInfos; }
        }


        [Constructible]
        public Thief() : base("the thief")
        {
            SetSkill(SkillName.Camping, 55.0, 78.0);
            SetSkill(SkillName.DetectHidden, 65.0, 88.0);
            SetSkill(SkillName.Hiding, 45.0, 68.0);
            SetSkill(SkillName.Archery, 65.0, 88.0);
            SetSkill(SkillName.Tracking, 65.0, 88.0);
            SetSkill(SkillName.Veterinary, 60.0, 83.0);
        }

        public override void InitSBInfo()
        {
            m_SBInfos.Add(new SBThief());
        }

        public override void InitOutfit()
        {
            base.InitOutfit();

            AddItem(new Server.Items.Shirt(Utility.RandomNeutralHue()));
            AddItem(new Server.Items.LongPants(Utility.RandomNeutralHue()));
            AddItem(new Server.Items.Dagger());
            AddItem(new Server.Items.ThighBoots());
        }

        [Constructible]
        public Thief(Serial serial) : base(serial)
        {
        }

        public override void Serialize(IGenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write((int) 0); // version
        }

        public override void Deserialize(IGenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();
        }
    }
}