// Generated File. DO NOT MODIFY BY HAND.

namespace Server.Items
{
  public class PinetreeLog : Log
  {
    [Constructible]
    public PinetreeLog() : this(1)
    {
    }


    [Constructible]
    public PinetreeLog(int amount) : base(CraftResource.Pinetree, amount)
    {
      this.Hue = 1132;
    }

    [Constructible]
    public PinetreeLog(Serial serial) : base(serial)
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

    public override bool Axe(Mobile from, BaseAxe axe)
    {
      if (!TryCreateBoards(from, 15, new PinetreeBoard()))
      {
        return false;
      }

      return true;
    }
  }
}
