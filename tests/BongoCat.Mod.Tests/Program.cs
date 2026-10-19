using BongoCatMod;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine("PASS " + name);
    passed++;
}

var ordinary = BalanceMath.SetBalance(10000, 3000, 500);
Check(ordinary == (3500, 3000), "Setting balance preserves spent history when it fits");
var capped = BalanceMath.SetBalance(BalanceMath.Maximum, 5732000, BalanceMath.Maximum);
Check(capped == (BalanceMath.Maximum, 0), "Setting maximum balance cannot overflow");
var reset = BalanceMath.MakeRoom(BalanceMath.Maximum, BalanceMath.Maximum, 1);
Check(reset == (0, 0), "Counter reset at the ceiling can earn again");
var room = BalanceMath.MakeRoom(BalanceMath.Maximum, 5732000, 1);
Check((long)room.Gained - room.Spent == (long)BalanceMath.Maximum - 5732000, "Rebasing preserves available points");
Check(room.Gained + 1 <= BalanceMath.Maximum, "Rebasing makes room for an actual click");
Check(BalanceMath.MakeRoom(100, 20, 2) == (100, 20), "Ordinary clicks keep their history");
Check(BalanceMath.MakeRoom(BalanceMath.Maximum, 0, 1) == (BalanceMath.Maximum, 0), "A full unspent balance stays capped");
var rejected = false;
try { BalanceMath.SetBalance(0, 0, -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
Check(rejected, "Negative balances are rejected");

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
