﻿using System;
using System.Diagnostics;
using Server.Mobiles;
using ZuluContent.Zulu.Engines.Magic;
using ZuluContent.Zulu.Engines.Magic.Enchantments;

namespace Server.Commands
{
    public class BenchmarkHooks
    {
        public static void Initialize()
        {
            CommandSystem.Register("BenchmarkHooks", AccessLevel.Owner, BenchmarkHooks_OnCommand);
        }

        [Usage("BenchmarkHooks [iterations]")]
        [Description("Run a benchmark on the hooks system.")]
        private static async void BenchmarkHooks_OnCommand(CommandEventArgs eventArgs)
        {
            if (eventArgs.Arguments.Length != 1)
                return;
            
            var iterations = eventArgs.GetUInt32(0);
            
            var elapsed = TimeSpan.FromTicks(0);

            elapsed += RunBench(
                "EnchantmentDictionary.Set",
                iterations, 
                (i, enchanted) => enchanted.Enchantments.Set((DexBonus e) => e.Value = i)
            );

            await Timer.Pause(1_000);
            
            elapsed += RunBench(
                "EnchantmentDictionary.Get",
                iterations, 
                (_, enchanted) => enchanted.Enchantments.Get((DexBonus e) => e.Value)
            );

            await Timer.Pause(1_000);
            
            elapsed += RunBench(
                "EnchantmentHooks.FireOrderedHook",
                iterations, 
                (i, enchanted) =>
                {
                    var val = 10.0;
                    (enchanted as Mobile).FireOrderedHook(h => h.OnHeal(enchanted, enchanted, null, ref val));
                });

            elapsed += RunBench(
                "EnchantmentHooks.FireUnorderedHook",
                iterations, 
                (i, enchanted) =>
                {
                    var val = 10.0;
                    (enchanted as Mobile).FireHook(h => h.OnHeal(enchanted, enchanted, null, ref val));
                });

            Console.WriteLine($"Finished all benchmarks in ({elapsed.TotalSeconds:F6} seconds).");
        }


        private static TimeSpan RunBench(string name, uint iterations, Action<int, BaseCreature> benchFunc)
        {
            var subject = new Rat();
            
            var watch = new Stopwatch();
            watch.Start();
            
            for (var i = 0; i < iterations; i++)
            {
                benchFunc(i, subject);
            }

            watch.Stop();

            var ticks = (double)watch.Elapsed.Ticks;
            var totalMs = ticks / TimeSpan.TicksPerMillisecond;
            
            var avgTicks = ticks / iterations;
            var avgMs = avgTicks / TimeSpan.TicksPerMillisecond;
            
            Console.WriteLine(
                $"Ran {iterations} of {name} " +
                $"iterations took {ticks} ticks ({totalMs} ms) " +
                $"with an average of {avgTicks} ticks ({avgMs} ms) per iteration."
            );
            
            subject.Delete();

            return watch.Elapsed;
        }
    }
}