namespace Server.Items
{
  [FlipableAttribute(0x1081, 0x1082)]
  public class WyrmLeather : BaseLeather
  {
    [Constructible]
    public WyrmLeather() : this(1)
    {
    }


    [Constructible]
    public WyrmLeather(int amount) : base(CraftResource.WyrmLeather, amount)
    {
      this.Hue = 2747;
    }

    [Constructible]
    public WyrmLeather(Serial serial) : base(serial)
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
