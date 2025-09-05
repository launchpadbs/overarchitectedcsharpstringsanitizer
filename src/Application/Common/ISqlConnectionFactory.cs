using System.Data;

namespace FlashAssessment.Application.Common;

public interface ISqlConnectionFactory
{
    IDbConnection CreateOpenConnection();
}


