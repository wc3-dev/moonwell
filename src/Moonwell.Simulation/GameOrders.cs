using System.Collections.Generic;

namespace Moonwell
{
    /// <summary>
    /// The game's order ids, as its OrderId gave them for every order string of the ability data and the seven base orders
    /// (3.0.1.24371, seen 11-14): 272 ids, in any case of the string, "challengingcall" giving taunt's. The 28 strings the game
    /// read 0 for are left out, so they read 0 as any string it does not know.
    /// </summary>
    internal static class GameOrders
    {
        // One string read once, not a table literal, which CSharp.lua would make one call with hundreds of arguments.
        // By id; of two strings with one id, the game's name for it comes first.
        private const string Table =
            "smart 851971 stop 851972 attack 851983 attackground 851984 move 851986 patrol 851990 holdposition "
            + "851993 build 851994 humanbuild 851995 orcbuild 851996 nightelfbuild 851997 undeadbuild 851998 harvest "
            + "852018 repair 852024 repairon 852025 repairoff 852026 revive 852039 selfdestruct 852040 load 852046 "
            + "unload 852047 loadcorpse 852050 defend 852055 undefend 852056 dispel 852057 flare 852060 heal 852063 "
            + "healon 852064 healoff 852065 innerfire 852066 innerfireon 852067 innerfireoff 852068 invisibility "
            + "852069 militia 852072 militiaoff 852073 polymorph 852074 slow 852075 slowon 852076 slowoff 852077 "
            + "townbellon 852082 townbelloff 852083 avatar 852086 blizzard 852089 divineshield 852090 undivineshield "
            + "852091 holybolt 852092 massteleport 852093 resurrection 852094 thunderbolt 852095 thunderclap 852096 "
            + "waterelemental 852097 battlestations 852099 berserk 852100 bloodlust 852101 bloodluston 852102 "
            + "bloodlustoff 852103 devour 852104 evileye 852105 ensnare 852106 healingward 852109 lightningshield "
            + "852110 purge 852111 standdown 852113 stasistrap 852114 chainlightning 852119 earthquake 852121 farsight "
            + "852122 mirrorimage 852123 shockwave 852125 spiritwolf 852126 stomp 852127 whirlwind 852128 windwalk "
            + "852129 ambush 852131 autodispel 852132 autodispelon 852133 autodispeloff 852134 bearform 852138 "
            + "unbearform 852139 corrosivebreath 852140 loadarcher 852142 mounthippogryph 852143 cyclone 852144 "
            + "detonate 852145 eattree 852146 entangle 852147 faeriefire 852149 faeriefireon 852150 faeriefireoff "
            + "852151 ravenform 852155 unravenform 852156 rejuvination 852160 renew 852161 renewon 852162 renewoff "
            + "852163 roar 852164 root 852165 unroot 852166 entanglingroots 852171 flamingarrows 852174 "
            + "unflamingarrows 852175 forceofnature 852176 immolation 852177 unimmolation 852178 manaburn 852179 "
            + "metamorphosis 852180 scout 852181 sentinel 852182 starfall 852183 tranquility 852184 antimagicshell "
            + "852186 cannibalize 852188 cripple 852189 curse 852190 curseon 852191 curseoff 852192 freezingbreath "
            + "852195 possession 852196 raisedead 852197 raisedeadon 852198 raisedeadoff 852199 restoration 852202 "
            + "restorationon 852203 restorationoff 852204 sacrifice 852205 stoneform 852206 unstoneform 852207 "
            + "unholyfrenzy 852209 unsummon 852210 web 852211 webon 852212 weboff 852213 animatedead 852217 "
            + "carrionswarm 852218 darkritual 852219 darksummoning 852220 deathanddecay 852221 deathcoil 852222 "
            + "deathpact 852223 frostarmor 852225 frostnova 852226 sleep 852227 darkconversion 852228 inferno 852232 "
            + "rainoffire 852238 soulpreservation 852242 creepdevour 852247 creepthunderbolt 852252 creepthunderclap "
            + "852253 poisonarrowstarg 852254 poisonarrows 852255 unpoisonarrows 852256 frostarmoron 852458 "
            + "frostarmoroff 852459 awaken 852466 nagabuild 852467 cloudoffog 852473 controlmagic 852474 magicdefense "
            + "852478 magicundefense 852479 magicleash 852480 spellsteal 852483 spellstealon 852484 spellstealoff "
            + "852485 banish 852486 flamestrike 852488 summonphoenix 852489 corporealform 852493 uncorporealform "
            + "852494 etherealform 852496 unetherealform 852497 spiritlink 852499 unstableconcoction 852500 "
            + "healingwave 852501 hex 852502 voodoo 852503 ward 852504 coupletarget 852507 decouple 852509 grabtree "
            + "852511 manaflareon 852512 manaflareoff 852513 phaseshift 852514 phaseshifton 852515 phaseshiftoff "
            + "852516 taunt 852520 challengingcall 852520 vengeance 852521 vengeanceon 852522 vengeanceoff 852523 "
            + "blink 852525 fanofknives 852526 shadowstrike 852527 spiritofvengeance 852528 burrow 852533 unburrow "
            + "852534 replenish 852542 replenishon 852543 replenishoff 852544 replenishlife 852545 replenishlifeon "
            + "852546 replenishlifeoff 852547 replenishmana 852548 replenishmanaon 852549 replenishmanaoff 852550 "
            + "carrionscarabs 852551 carrionscarabson 852552 carrionscarabsoff 852553 impale 852555 locustswarm 852556 "
            + "frenzy 852561 frenzyon 852562 frenzyoff 852563 mindrot 852565 spellshield 852571 attributemodskill "
            + "852576 blackarrow 852577 blackarrowon 852578 blackarrowoff 852579 breathoffire 852580 charm 852581 doom "
            + "852583 drunkenhaze 852585 elementalfury 852586 forkedlightning 852587 howlofterror 852588 manashieldon "
            + "852589 manashieldoff 852590 monsoon 852591 silence 852592 stampede 852593 summongrizzly 852594 "
            + "summonquillbeast 852595 summonwareagle 852596 tornado 852597 wateryminion 852598 battleroar 852599 "
            + "channel 852600 parasite 852601 parasiteon 852602 parasiteoff 852603 submerge 852604 unsubmerge 852605 "
            + "neutralspell 852630 clusterrockets 852652 robogoblin 852656 unrobogoblin 852657 summonfactory 852658 "
            + "acidbomb 852662 chemicalrage 852663 healingspray 852664 transmute 852665 soulburn 852668 volcano 852669 "
            + "incineratearrow 852670 incineratearrowon 852671 unholyfrenzyaoe 852675 sweepingstrike 852680 "
            + "heroicslash 852684 valiantcharge 852685 inspirecourage 852689 headsplitter 852691 heroicchallenge "
            + "852694 guidinghand 852695 unguidinghand 852696 warcry 852701 consecration 852702 undeadvengeance 852703 "
            + "cleansingfire 852704 batteringram 852705 righteousfury 852706 soullantern 852708 unsoullantern 852709 "
            + "witheringfire 852711";

        private static Dictionary<string, int>? ids;
        private static Dictionary<int, string>? names;

        /// <summary>The id of an order string in any case, as the game's OrderId gives it; 0 for one it has no id for.</summary>
        internal static int IdOf(string order)
        {
            Read();
            return ids!.TryGetValue(order.ToLowerInvariant(), out var id) ? id : 0;
        }

        /// <summary>The game's string of an order id, as its OrderId2String gives it; "" for one the table does not have.</summary>
        internal static string NameOf(int id)
        {
            Read();
            return names!.TryGetValue(id, out var name) ? name : "";
        }

        /// <summary>Reads the table on first use.</summary>
        private static void Read()
        {
            if (ids != null)
            {
                return;
            }

            var parts = Table.Split(' ');
            ids = new Dictionary<string, int>();
            names = new Dictionary<int, string>();
            for (var i = 0; i + 1 < parts.Length; i += 2)
            {
                var id = int.Parse(parts[i + 1]);
                ids[parts[i]] = id;
                if (!names.ContainsKey(id))
                {
                    names[id] = parts[i];
                }
            }
        }
    }
}
