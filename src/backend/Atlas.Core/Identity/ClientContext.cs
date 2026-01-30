namespace Atlas.Core.Identity;

public enum ClientType
{
    WebH5 = 0,
    Mobile = 1,
    Backend = 2
}

public enum ClientPlatform
{
    Web = 0,
    Android = 1,
    iOS = 2
}

public enum ClientChannel
{
    Browser = 0,
    App = 1
}

public enum ClientAgent
{
    Chrome = 0,
    Edge = 1,
    Safari = 2,
    Firefox = 3,
    Other = 4
}

public sealed record ClientContext(
    ClientType ClientType,
    ClientPlatform ClientPlatform,
    ClientChannel ClientChannel,
    ClientAgent ClientAgent);
