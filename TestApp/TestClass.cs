// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace TestApp;


    public interface ITest
    {
        string DoSomething();
    }
    public class TestClass : ITest
    {
        public string DoSomething()
        {
            return "TestClass";
        }
    }

    public class TestNo2Class : ITest
    {
        public string DoSomething()
        {
            return "TestNo2Class";
        }
    }

    public class NormalClass
    {
        public string DoABarrelRoll()
        {
            return "Did a barrell roll";
        }
    }


