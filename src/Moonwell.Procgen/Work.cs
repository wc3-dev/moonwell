using System;
using System.Collections.Generic;

namespace Moonwell.Procgen
{
    /// <summary>
    /// Work in passes of units, each pass named for the step it belongs to, run a budget at a time. A unit's cost is a fixed
    /// estimate in microseconds, never a time measured, so each run does the same units on every machine.
    /// </summary>
    internal sealed class Work
    {
        private readonly List<Pass> passes = new List<Pass>();

        /// <summary>The pass running.</summary>
        private int at;

        /// <summary>Its next unit.</summary>
        private int next;

        /// <summary>Whether the pass running has been begun: its exact cost read.</summary>
        private bool begun;

        /// <summary>The cost of the units run so far.</summary>
        private float spent;

        /// <summary>The cost of every unit, as estimated so far.</summary>
        private float total;

        /// <summary>The share done, and the cost spent, when the estimate last changed: the rest of the bar is the rest of the work.</summary>
        private float fromShare;

        private float fromSpent;

        /// <summary>Whether every pass has run.</summary>
        internal bool IsDone => at >= passes.Count;

        /// <summary>The step of the pass running, or of the last once all have run; empty with no passes.</summary>
        internal string Step => passes.Count == 0 ? "" : passes[at < passes.Count ? at : passes.Count - 1].Step;

        /// <summary>The share done, from 0 to 1, never back; 1 only once every pass has run.</summary>
        internal float Progress
        {
            get
            {
                if (IsDone)
                {
                    return 1f;
                }

                var left = total - fromSpent;
                var share = left > 0f ? fromShare + (1f - fromShare) * (spent - fromSpent) / left : fromShare;
                return share < 0.9999f ? share : 0.9999f;
            }
        }

        /// <summary>
        /// Adds a pass of units, each costing <paramref name="each"/>, or <paramref name="costOf"/> its own when they differ. When
        /// the units' costs are not known until the pass begins, <paramref name="exact"/> gives the pass's whole cost then, and
        /// the rest of the bar is spread over the rest of the work from there.
        /// </summary>
        internal void Add(string step, int units, float each, Action<int, int> run, Func<int, float>? costOf = null, Func<float>? exact = null)
        {
            passes.Add(new Pass(step, units, each, run, costOf, exact));
            total += units * each;
        }

        /// <summary>Runs units until the budget is spent, at least one, and gives whether every pass has run.</summary>
        internal bool Run(float budget)
        {
            var ranAny = false;
            while (at < passes.Count)
            {
                var pass = passes[at];
                if (!begun)
                {
                    begun = true;
                    if (pass.Exact != null)
                    {
                        Reestimate(pass.Units * pass.Each, pass.Exact());
                    }
                }

                var end = next;
                var used = 0f;
                if (pass.CostOf == null)
                {
                    var fits = (int)(budget / pass.Each);
                    end = Math.Min(pass.Units, next + (fits > 0 || ranAny ? fits : 1));
                    used = (end - next) * pass.Each;
                }
                else
                {
                    while (end < pass.Units)
                    {
                        var cost = pass.CostOf(end);
                        if (used + cost > budget && (ranAny || end > next))
                        {
                            break;
                        }

                        used += cost;
                        end++;
                    }
                }

                if (end > next)
                {
                    pass.Run(next, end);
                    ranAny = true;
                    spent += used;
                    budget -= used;
                    next = end;
                }

                if (next < pass.Units)
                {
                    return false;
                }

                at++;
                next = 0;
                begun = false;
            }

            return true;
        }

        /// <summary>A pass's estimated cost became its exact one: the bar keeps its place and the rest of it is the rest of the work.</summary>
        private void Reestimate(float estimate, float exact)
        {
            fromShare = Progress;
            fromSpent = spent;
            total += exact - estimate;
        }

        private sealed class Pass
        {
            internal readonly string Step;
            internal readonly int Units;
            internal readonly float Each;
            internal readonly Action<int, int> Run;
            internal readonly Func<int, float>? CostOf;
            internal readonly Func<float>? Exact;

            internal Pass(string step, int units, float each, Action<int, int> run, Func<int, float>? costOf, Func<float>? exact)
            {
                Step = step;
                Units = units;
                Each = each;
                Run = run;
                CostOf = costOf;
                Exact = exact;
            }
        }
    }
}
