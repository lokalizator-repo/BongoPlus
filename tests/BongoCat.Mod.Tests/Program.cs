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

var policy = new AutomationPolicy { Limit = 1000 };
Check(!policy.Observe("friend:skin", true, 0), "A newly observed ready chest waits for stable state");
Check(policy.Observe("friend:skin", true, 2), "A stable ready chest can receive one gift");
policy.Record("friend:skin", 500);
Check(!policy.Observe("friend:skin", true, 10), "Repeated ready events cannot duplicate a gift");
policy.Observe("friend:skin", false, 11);
Check(!policy.Observe("friend:skin", true, 12) && policy.Observe("friend:skin", true, 14), "A new chest cycle becomes eligible");
Check(policy.CanSpend(500) && !policy.CanSpend(501) && !policy.CanSpend(0), "Spending respects the remaining budget");
policy.Record("self", 500);
Check(!policy.CanSpend(1), "Budget exhaustion stops subsequent spending");
policy.NewLobby();
Check(policy.Spent == 1000 && !policy.CanSpend(1), "Changing lobbies cannot reset the spending budget");
Console.WriteLine($"{passed} checks passed.");
