package Animal

MountainFrenziedOstard: {
	BaseType:             "BaseMount"
	Name:                 "a mountain frenzied ostard"
	CorpseNameOverride:   "corpse of a mountain frenzied ostard"
	Str:                  130
	Int:                  35
	Dex:                  180
	BaseSoundID:          629
	Body:                 218
	ItemID:								16036
	CreatureType:         "Animal"
	VirtualArmor:         10
	HideType:             "Ostard"
	Hides:                4
	HitsMaxSeed:          130
	Hue:                  1160
	ManaMaxSeed:          0
	MinTameSkill:         95
	ProvokeSkillOverride: 110
	StamMaxSeed:          50
	Tamable:              true
	Skills: {
		Parry:       80
		MagicResist: 70
		Tactics:     100
		Wrestling:   140
	}
	Attack: {
		Speed: 25
		Damage: {
			Min: 10
			Max: 45
		}
		HitSound:  595
		MissSound: 597
	}
}
