using BongoCatMod;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

var ordinary = BalanceMath.SetBalance(10000, 3000, 500);
Check(ordinary == (10000, 9500), "Lowering balance increases spent without decreasing earned clicks");
var increased = BalanceMath.SetBalance(10000, 3000, 8000);
Check(increased == (11000, 3000), "Raising balance increases earned clicks without decreasing spent");
Check(BalanceMath.SetBalance(10000, 3000, 7000) == (10000, 3000), "An unchanged balance needs no stat update");
Check(BalanceMath.SetBalance(10000, 3000, 0) == (10000, 10000), "Setting zero respects increment-only counters");
var capped = BalanceMath.SetBalance(BalanceMath.Maximum, 5732000, BalanceMath.Maximum - 5732000);
Check(capped == (BalanceMath.Maximum, 5732000), "Maximum attainable balance preserves spent history");
var overLimit = false;
try { BalanceMath.SetBalance(BalanceMath.Maximum, 5732000, BalanceMath.Maximum); }
catch (ArgumentOutOfRangeException) { overLimit = true; }
Check(overLimit, "A balance requiring a decreasing counter is rejected before any write");
var resetBlocked = false;
try { BalanceMath.SetBalance(BalanceMath.Maximum, BalanceMath.Maximum, 1); }
catch (ArgumentOutOfRangeException) { resetBlocked = true; }
Check(resetBlocked, "A fully exhausted pair of Steam counters cannot be rebased");
var rejected = false;
try { BalanceMath.SetBalance(0, 0, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "Negative balances are rejected");

var restore = BalanceMath.SetBalance(11000, 3000, 7000);
Check(restore == (11000, 4000), "Restoring an earlier balance preserves increasing history");
var random = new Random(3419430);
var allPlansValid = true;
for (var i = 0; i < 5000; i++)
{
    var gained = random.Next(1, BalanceMath.Maximum);
    var spent = random.Next(0, gained);
    var target = random.Next(0, BalanceMath.Maximum - spent);
    var next = BalanceMath.SetBalance(gained, spent, target);
    allPlansValid &= next.Gained >= gained && next.Spent >= spent && next.Gained - next.Spent == target &&
        (next.Gained == gained || next.Spent == spent);
}
Check(allPlansValid, "5000 balance plans satisfy Steam monotonicity and change at most one counter");

var policy = new AutomationPolicy();
Check(policy.Observe("friend:skin", true), "An advertised ready chest is eligible without an artificial delay");
policy.Record("friend:skin");
Check(!policy.Observe("friend:skin", true), "Repeated ready events cannot duplicate a gift");
Check(!policy.NeedsUiReset("friend:skin"), "The current paid request cannot reset its own click guard");
Check(policy.Observe("friend:emote", true), "A cosmetic gift cannot block the same recipient's emote chest");
policy.Record("friend:emote");
policy.Observe("friend:skin", false);
Check(policy.Observe("friend:skin", true) && policy.NeedsUiReset("friend:skin"), "An explicit new chest cycle releases a stale UI click guard");
Check(!policy.Observe("friend:emote", true), "Resetting the cosmetic cycle does not release the emote request");
Check(policy.Observe("other:skin", true), "A gift to one player cannot block a different recipient");
policy.Record("friend:skin");
Check(!policy.NeedsUiReset("friend:skin"), "Sending the next gift consumes the UI reset permission");
policy.NewLobby();
Check(policy.Observe("friend:skin", true) && !policy.NeedsUiReset("friend:skin"), "A new lobby clears old requests without resetting an unrelated UI click");
SettingsTests.Run(Check);
WheelLayoutTests.Run(Check);
ControlArtTests.Run(Check);
if (args.Length == 2 && args[0] == "--preview") ControlArtTests.ExportPreview(args[1]);
Console.WriteLine($"{passed} checks passed.");
