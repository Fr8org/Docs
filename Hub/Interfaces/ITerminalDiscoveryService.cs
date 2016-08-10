using Fr8.Infrastructure.Data.DataTransferObjects;
using System.Threading.Tasks;

namespace Hub.Interfaces
{
    public interface ITerminalDiscoveryService
    {
        Task DiscoverAll();
        Task<bool> Discover(string terminalUrl, bool isUserInitiated);
        Task SaveOrRegister(TerminalDTO terminal);
    }
}