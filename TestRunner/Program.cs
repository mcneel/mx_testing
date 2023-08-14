using System.Collections;


class Program
{
  static void Main()
  {

  }

}



public interface ITestRunner
{
  void InitAssembly(Hashtable baseSettings);

  string LoadTests();

  string ExploreTests();
  string ExploreTests(string filter);

  string RunTests();
  string RunTests(string filter);

  string RunTestByName(string name);
}


