using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes the set of deploy options passed to the DAC package process.
    /// </summary>
    public class SqlDeploymentDatabasePackageDeployOptions
    {

        public SqlDeploymentExpression? AdditionalDeploymentContributorArguments { get; set; }

        public SqlDeploymentExpression? AdditionalDeploymentContributorPaths { get; set; }

        public SqlDeploymentExpression? AdditionalDeploymentContributors { get; set; }

        public SqlDeploymentExpression? AllowDropBlockingAssemblies { get; set; }

        public SqlDeploymentExpression? AllowIncompatiblePlatform { get; set; }

        public SqlDeploymentExpression? AllowUnsafeRowLevelSecurityDataMovement { get; set; }

        public SqlDeploymentExpression? BackupDatabaseBeforeChanges { get; set; }

        public SqlDeploymentExpression? BlockOnPossibleDataLoss { get; set; }

        public SqlDeploymentExpression? BlockWhenDriftDetected { get; set; }

        public SqlDeploymentExpression? CommandTimeout { get; set; }

        public SqlDeploymentExpression? CommentOutSetVarDeclarations { get; set; }

        public SqlDeploymentExpression? CompareUsingTargetCollation { get; set; }

        public SqlDeploymentExpression? CreateNewDatabase { get; set; }

        public SqlDeploymentExpression? DatabaseLockTimeout { get; set; }

        //public DacAzureDatabaseSpecification DatabaseSpecification { get; set; }

        public SqlDeploymentExpression? DeployDatabaseInSingleUserMode { get; set; }

        public SqlDeploymentExpression? DisableAndReenableDdlTriggers { get; set; }

        public SqlDeploymentExpression? DoNotAlterChangeDataCaptureObjects { get; set; }

        public SqlDeploymentExpression? DoNotAlterReplicatedObjects { get; set; }

        public SqlDeploymentExpression[] DoNotDropObjectTypes { get; set; }

        public SqlDeploymentExpression? DropConstraintsNotInSource { get; set; }

        public SqlDeploymentExpression? DropDmlTriggersNotInSource { get; set; }

        public SqlDeploymentExpression? DropExtendedPropertiesNotInSource { get; set; }

        public SqlDeploymentExpression? DropIndexesNotInSource { get; set; }

        public SqlDeploymentExpression? DropObjectsNotInSource { get; set; }

        public SqlDeploymentExpression? DropPermissionsNotInSource { get; set; }

        public SqlDeploymentExpression? DropRoleMembersNotInSource { get; set; }

        public SqlDeploymentExpression? DropStatisticsNotInSource { get; set; }

        public SqlDeploymentExpression[] ExcludeObjectTypes { get; set; }

        public SqlDeploymentExpression? GenerateSmartDefaults { get; set; }

        public SqlDeploymentExpression? IgnoreAnsiNulls { get; set; }

        public SqlDeploymentExpression? IgnoreAuthorizer { get; set; }

        public SqlDeploymentExpression? IgnoreColumnCollation { get; set; }

        public SqlDeploymentExpression? IgnoreColumnOrder { get; set; }

        public SqlDeploymentExpression? IgnoreComments { get; set; }

        public SqlDeploymentExpression? IgnoreCryptographicProviderFilePath { get; set; }

        public SqlDeploymentExpression? IgnoreDdlTriggerOrder { get; set; }

        public SqlDeploymentExpression? IgnoreDdlTriggerState { get; set; }

        public SqlDeploymentExpression? IgnoreDefaultSchema { get; set; }

        public SqlDeploymentExpression? IgnoreDmlTriggerOrder { get; set; }

        public SqlDeploymentExpression? IgnoreDmlTriggerState { get; set; }

        public SqlDeploymentExpression? IgnoreExtendedProperties { get; set; }

        public SqlDeploymentExpression? IgnoreFileAndLogFilePath { get; set; }

        public SqlDeploymentExpression? IgnoreFilegroupPlacement { get; set; }

        public SqlDeploymentExpression? IgnoreFileSize { get; set; }

        public SqlDeploymentExpression? IgnoreFillFactor { get; set; }

        public SqlDeploymentExpression? IgnoreFullTextCatalogFilePath { get; set; }

        public SqlDeploymentExpression? IgnoreIdentitySeed { get; set; }

        public SqlDeploymentExpression? IgnoreIncrement { get; set; }

        public SqlDeploymentExpression? IgnoreIndexOptions { get; set; }

        public SqlDeploymentExpression? IgnoreIndexPadding { get; set; }

        public SqlDeploymentExpression? IgnoreKeywordCasing { get; set; }

        public SqlDeploymentExpression? IgnoreLockHintsOnIndexes { get; set; }

        public SqlDeploymentExpression? IgnoreLoginSids { get; set; }

        public SqlDeploymentExpression? IgnoreNotForReplication { get; set; }

        public SqlDeploymentExpression? IgnoreObjectPlacementOnPartitionScheme { get; set; }

        public SqlDeploymentExpression? IgnorePartitionSchemes { get; set; }

        public SqlDeploymentExpression? IgnorePermissions { get; set; }

        public SqlDeploymentExpression? IgnoreQuotedIdentifiers { get; set; }

        public SqlDeploymentExpression? IgnoreRoleMembership { get; set; }

        public SqlDeploymentExpression? IgnoreRouteLifetime { get; set; }

        public SqlDeploymentExpression? IgnoreSemicolonBetweenStatements { get; set; }

        public SqlDeploymentExpression? IgnoreTableOptions { get; set; }

        public SqlDeploymentExpression? IgnoreUserSettingsObjects { get; set; }

        public SqlDeploymentExpression? IgnoreWhitespace { get; set; }

        public SqlDeploymentExpression? IgnoreWithNocheckOnCheckConstraints { get; set; }

        public SqlDeploymentExpression? IgnoreWithNocheckOnForeignKeys { get; set; }

        public SqlDeploymentExpression? IncludeCompositeObjects { get; set; }

        public SqlDeploymentExpression? IncludeTransactionalScripts { get; set; }

        public SqlDeploymentExpression? LongRunningCommandTimeout { get; set; }

        public SqlDeploymentExpression? NoAlterStatementsToChangeClrTypes { get; set; }

        public SqlDeploymentExpression? PopulateFilesOnFileGroups { get; set; }

        public SqlDeploymentExpression? RegisterDataTierApplication { get; set; }

        public SqlDeploymentExpression? RunDeploymentPlanExecutors { get; set; }

        public SqlDeploymentExpression? ScriptDatabaseCollation { get; set; }

        public SqlDeploymentExpression? ScriptDatabaseCompatibility { get; set; }

        public SqlDeploymentExpression? ScriptDatabaseOptions { get; set; }

        public SqlDeploymentExpression? ScriptDeployStateChecks { get; set; }

        public SqlDeploymentExpression? ScriptFileSize { get; set; }

        public SqlDeploymentExpression? ScriptNewConstraintValidation { get; set; }

        public SqlDeploymentExpression? ScriptRefreshModule { get; set; }

        public IDictionary<string, SqlDeploymentExpression> SqlCommandVariableValues { get; set; } = new Dictionary<string, SqlDeploymentExpression>();

        public SqlDeploymentExpression? TreatVerificationErrorsAsWarnings { get; set; }

        public SqlDeploymentExpression? UnmodifiableObjectWarnings { get; set; }

        public SqlDeploymentExpression? VerifyCollationCompatibility { get; set; }

        public SqlDeploymentExpression? VerifyDeployment { get; set; }

    }

}
