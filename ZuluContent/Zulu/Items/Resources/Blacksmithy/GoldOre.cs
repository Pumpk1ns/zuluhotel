// Generated File. DO NOT MODIFY BY HAND.

namespace Server.Items
{
  public class GoldOre : BaseOre
  {
    [Constructible]
    public GoldOre() : this(1)
    {
    }


    [Constructible]
    public GoldOre(int amount) : base(CraftResource.Gold, amount)
    {
      this.Hue = 2793;
    }

    [Constructible]
    public GoldOre(Serial serial) : base(serial)
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

    public override BaseIngot GetIngot()
    {
      return new GoldIngot();
    }
  }
}
