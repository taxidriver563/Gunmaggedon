using System;
using GunmageddonAudio;
public static class SearchlightClaimsTests
{
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 public static void Main()
 {
  var claims=new SearchlightClaims<object,object,object>();var team=new object();var enemy=new object();var plane=new object();var otherPlane=new object();var first=new object();var second=new object();bool alive=true;
  Func<object,object,object,bool> valid=(owner,t,p)=>owner!=first||alive;
  Check(claims.Take(team,plane,first,0,valid),"First light reserves aircraft");
  Check(!claims.Take(team,plane,second,.1f,valid),"Other light cannot duplicate");
  Check(claims.Take(team,otherPlane,second,.1f,valid),"Other aircraft remains available");
  Check(claims.Take(enemy,plane,second,.1f,valid),"Different factions have independent claims");
  Check(claims.Take(team,plane,first,1,valid),"Holder renews");
  Check(!claims.Available(team,plane,second,1.3f,valid),"Renewal extends lease");
  alive=false;Check(claims.Take(team,plane,second,1.4f,valid),"Dead/invalid holder reclaimed immediately");
  claims.Release(first);Check(!claims.Available(team,plane,first,1.5f,valid),"Old holder cannot release replacement claim");
  claims.Release(second);Check(claims.Take(team,plane,first,2,valid),"Release frees aircraft");
  alive=true;Check(claims.Take(team,plane,second,3.25f,valid),"Unrenewed token expires at boundary");
  Check(!claims.Available(null,plane,first,4,valid),"Missing team rejected");
  claims.Clear();Check(claims.Take(team,plane,first,0,valid),"Shutdown reset supports new mission clock");
  Console.WriteLine("SearchlightClaimsTests passed: exclusive claims, independent aircraft/factions, renewal, release, invalid owners, expiration and mission reset.");
 }
}
