using Tianci.OA.Domain.Common;

namespace Tianci.OA.UnitTests;

public sealed class DomainContractTests
{
    [Fact]
    public void Persisted_status_values_match_database_contract()
    {
        Assert.Equal(3, (byte)EmployeeStatus.Terminated);
        Assert.Equal(5, (byte)ResumeStatus.OfferPending);
        Assert.Equal(2, (byte)ContractStatus.Active);
        Assert.Equal(2, (byte)WorkflowStatus.Completed);
    }
}
