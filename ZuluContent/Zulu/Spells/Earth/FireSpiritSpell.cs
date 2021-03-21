using System;
using System.Collections;
using Server;
using Server.Network;
using Server.Items;
using Server.Targeting;
using Server.Mobiles;
using Server.Spells;

namespace Scripts.Zulu.Spells.Earth
{
    public class FireSpiritSpell : EarthSpell
    {
        public override TimeSpan CastDelayBase
        {
            get { return TimeSpan.FromSeconds(0); }
        }

        public override double RequiredSkill
        {
            get { return 120.0; }
        }

        public override int RequiredMana
        {
            get { return 20; }
        }

        public FireSpiritSpell(Mobile caster, Item spellItem) : base(caster, spellItem)
        {
        }

        public override void OnCast()
        {
            if (!CheckSequence()) goto Return;

            var duration = TimeSpan.FromSeconds(2 * Caster.Skills[DamageSkill].Fixed / 4);

            SpellHelper.Summon(new FireElementalLord(), Caster, 0x217, duration, false);

            Return:
            FinishSequence();
        }
    }
}