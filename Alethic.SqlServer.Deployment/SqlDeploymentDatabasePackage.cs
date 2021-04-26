using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.SqlServer.Dac;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a database DACPAC deployment.
    /// </summary>
    public class SqlDeploymentDatabasePackage
    {

        /// <summary>
        /// Gets the path to the DACPAC to be deployed to the database.
        /// </summary>
        public SqlDeploymentExpression Source { get; set; }

        /// <summary>
        /// Gets the path to the publish profile XML file to load during compilation.
        /// </summary>
        public SqlDeploymentExpression? ProfileSource { get; set; }

        /// <summary>
        /// Gets the set of additional options to be passed into the database package.
        /// </summary>
        public SqlDeploymentDatabasePackageDeployOptions DeployOptions { get; set; } = new SqlDeploymentDatabasePackageDeployOptions();

        /// <summary>
        /// Compiles the package deployment configuration.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context, string name)
        {
            if (Source != null)
            {
                // ensure source path is absolute
                var source = Source.Expand(context);
                if (Path.IsPathRooted(source) == false)
                    source = Path.Combine(context.RelativeRoot, source);

                yield return new SqlDeploymentDatabasePackageAction(context.InstanceName, name, source, LoadProfile(context));
            }
        }

        /// <summary>
        /// Loads the profile.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        DacProfile LoadProfile(SqlDeploymentCompileContext context)
        {
            var profile = new DacProfile();

            // load external profile source
            if (ProfileSource != null)
            {
                // ensure path is rooted
                var source = ProfileSource.Value.Expand(context);
                if (Path.IsPathRooted(source) == false)
                    source = Path.Combine(context.RelativeRoot, source);

                profile = DacProfile.Load(source);
            }

            if (DeployOptions.AdditionalDeploymentContributorArguments != null)
                profile.DeployOptions.AdditionalDeploymentContributorArguments = DeployOptions.AdditionalDeploymentContributorArguments.Value.Expand<string>(context);
            if (DeployOptions.AdditionalDeploymentContributorPaths != null)
                profile.DeployOptions.AdditionalDeploymentContributorPaths = DeployOptions.AdditionalDeploymentContributorPaths.Value.Expand<string>(context);
            if (DeployOptions.AdditionalDeploymentContributors != null)
                profile.DeployOptions.AdditionalDeploymentContributors = DeployOptions.AdditionalDeploymentContributors.Value.Expand<string>(context);
            if (DeployOptions.AllowDropBlockingAssemblies != null)
                profile.DeployOptions.AllowDropBlockingAssemblies = DeployOptions.AllowDropBlockingAssemblies.Value.Expand<bool>(context);
            if (DeployOptions.AllowIncompatiblePlatform != null)
                profile.DeployOptions.AllowIncompatiblePlatform = DeployOptions.AllowIncompatiblePlatform.Value.Expand<bool>(context);
            if (DeployOptions.AllowUnsafeRowLevelSecurityDataMovement != null)
                profile.DeployOptions.AllowUnsafeRowLevelSecurityDataMovement = DeployOptions.AllowUnsafeRowLevelSecurityDataMovement.Value.Expand<bool>(context);
            if (DeployOptions.BackupDatabaseBeforeChanges != null)
                profile.DeployOptions.BackupDatabaseBeforeChanges = DeployOptions.BackupDatabaseBeforeChanges.Value.Expand<bool>(context);
            if (DeployOptions.BlockOnPossibleDataLoss != null)
                profile.DeployOptions.BlockOnPossibleDataLoss = DeployOptions.BlockOnPossibleDataLoss.Value.Expand<bool>(context);
            if (DeployOptions.BlockWhenDriftDetected != null)
                profile.DeployOptions.BlockWhenDriftDetected = DeployOptions.BlockWhenDriftDetected.Value.Expand<bool>(context);
            if (DeployOptions.CommandTimeout != null)
                profile.DeployOptions.CommandTimeout = DeployOptions.CommandTimeout.Value.Expand<int>(context);
            if (DeployOptions.CommentOutSetVarDeclarations != null)
                profile.DeployOptions.CommentOutSetVarDeclarations = DeployOptions.CommentOutSetVarDeclarations.Value.Expand<bool>(context);
            if (DeployOptions.CompareUsingTargetCollation != null)
                profile.DeployOptions.CompareUsingTargetCollation = DeployOptions.CompareUsingTargetCollation.Value.Expand<bool>(context);
            if (DeployOptions.CreateNewDatabase != null)
                profile.DeployOptions.CreateNewDatabase = DeployOptions.CreateNewDatabase.Value.Expand<bool>(context);
            if (DeployOptions.DatabaseLockTimeout != null)
                profile.DeployOptions.DatabaseLockTimeout = DeployOptions.DatabaseLockTimeout.Value.Expand<int>(context);
            //if (DeployOptions.DatabaseSpecification != null)
            //    profile.DeployOptions.DatabaseSpecification = DeployOptions.DatabaseSpecification.Value.Expand<bool>(context);
            if (DeployOptions.DeployDatabaseInSingleUserMode != null)
                profile.DeployOptions.DeployDatabaseInSingleUserMode = DeployOptions.DeployDatabaseInSingleUserMode.Value.Expand<bool>(context);
            if (DeployOptions.DisableAndReenableDdlTriggers != null)
                profile.DeployOptions.DisableAndReenableDdlTriggers = DeployOptions.DisableAndReenableDdlTriggers.Value.Expand<bool>(context);
            if (DeployOptions.DoNotAlterChangeDataCaptureObjects != null)
                profile.DeployOptions.DoNotAlterChangeDataCaptureObjects = DeployOptions.DoNotAlterChangeDataCaptureObjects.Value.Expand<bool>(context);
            if (DeployOptions.DoNotAlterReplicatedObjects != null)
                profile.DeployOptions.DoNotAlterReplicatedObjects = DeployOptions.DoNotAlterReplicatedObjects.Value.Expand<bool>(context);
            if (DeployOptions.DoNotDropObjectTypes != null)
                profile.DeployOptions.DoNotDropObjectTypes = DeployOptions.DoNotDropObjectTypes?.SelectMany(i => i.Expand<string>(context)?.Split(';').Select(i => ToObjectType(i))).ToArray();
            if (DeployOptions.DropConstraintsNotInSource != null)
                profile.DeployOptions.DropConstraintsNotInSource = DeployOptions.DropConstraintsNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropDmlTriggersNotInSource != null)
                profile.DeployOptions.DropDmlTriggersNotInSource = DeployOptions.DropDmlTriggersNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropExtendedPropertiesNotInSource != null)
                profile.DeployOptions.DropExtendedPropertiesNotInSource = DeployOptions.DropExtendedPropertiesNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropIndexesNotInSource != null)
                profile.DeployOptions.DropIndexesNotInSource = DeployOptions.DropIndexesNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropObjectsNotInSource != null)
                profile.DeployOptions.DropObjectsNotInSource = DeployOptions.DropObjectsNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropPermissionsNotInSource != null)
                profile.DeployOptions.DropPermissionsNotInSource = DeployOptions.DropPermissionsNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropRoleMembersNotInSource != null)
                profile.DeployOptions.DropRoleMembersNotInSource = DeployOptions.DropRoleMembersNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.DropStatisticsNotInSource != null)
                profile.DeployOptions.DropStatisticsNotInSource = DeployOptions.DropStatisticsNotInSource.Value.Expand<bool>(context);
            if (DeployOptions.ExcludeObjectTypes != null)
                profile.DeployOptions.ExcludeObjectTypes = DeployOptions.ExcludeObjectTypes?.SelectMany(i => i.Expand<string>(context)?.Split(';').Select(i => ToObjectType(i))).ToArray();
            if (DeployOptions.GenerateSmartDefaults != null)
                profile.DeployOptions.GenerateSmartDefaults = DeployOptions.GenerateSmartDefaults.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreAnsiNulls != null)
                profile.DeployOptions.IgnoreAnsiNulls = DeployOptions.IgnoreAnsiNulls.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreAuthorizer != null)
                profile.DeployOptions.IgnoreAuthorizer = DeployOptions.IgnoreAuthorizer.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreColumnCollation != null)
                profile.DeployOptions.IgnoreColumnCollation = DeployOptions.IgnoreColumnCollation.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreColumnOrder != null)
                profile.DeployOptions.IgnoreColumnOrder = DeployOptions.IgnoreColumnOrder.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreComments != null)
                profile.DeployOptions.IgnoreComments = DeployOptions.IgnoreComments.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreCryptographicProviderFilePath != null)
                profile.DeployOptions.IgnoreCryptographicProviderFilePath = DeployOptions.IgnoreCryptographicProviderFilePath.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreDdlTriggerOrder != null)
                profile.DeployOptions.IgnoreDdlTriggerOrder = DeployOptions.IgnoreDdlTriggerOrder.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreDdlTriggerState != null)
                profile.DeployOptions.IgnoreDdlTriggerState = DeployOptions.IgnoreDdlTriggerState.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreDefaultSchema != null)
                profile.DeployOptions.IgnoreDefaultSchema = DeployOptions.IgnoreDefaultSchema.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreDmlTriggerOrder != null)
                profile.DeployOptions.IgnoreDmlTriggerOrder = DeployOptions.IgnoreDmlTriggerOrder.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreDmlTriggerState != null)
                profile.DeployOptions.IgnoreDmlTriggerState = DeployOptions.IgnoreDmlTriggerState.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreExtendedProperties != null)
                profile.DeployOptions.IgnoreExtendedProperties = DeployOptions.IgnoreExtendedProperties.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreFileAndLogFilePath != null)
                profile.DeployOptions.IgnoreFileAndLogFilePath = DeployOptions.IgnoreFileAndLogFilePath.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreFilegroupPlacement != null)
                profile.DeployOptions.IgnoreFilegroupPlacement = DeployOptions.IgnoreFilegroupPlacement.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreFileSize != null)
                profile.DeployOptions.IgnoreFileSize = DeployOptions.IgnoreFileSize.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreFillFactor != null)
                profile.DeployOptions.IgnoreFillFactor = DeployOptions.IgnoreFillFactor.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreFullTextCatalogFilePath != null)
                profile.DeployOptions.IgnoreFullTextCatalogFilePath = DeployOptions.IgnoreFullTextCatalogFilePath.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreIdentitySeed != null)
                profile.DeployOptions.IgnoreIdentitySeed = DeployOptions.IgnoreIdentitySeed.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreIncrement != null)
                profile.DeployOptions.IgnoreIncrement = DeployOptions.IgnoreIncrement.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreIndexOptions != null)
                profile.DeployOptions.IgnoreIndexOptions = DeployOptions.IgnoreIndexOptions.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreIndexPadding != null)
                profile.DeployOptions.IgnoreIndexPadding = DeployOptions.IgnoreIndexPadding.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreKeywordCasing != null)
                profile.DeployOptions.IgnoreKeywordCasing = DeployOptions.IgnoreKeywordCasing.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreLockHintsOnIndexes != null)
                profile.DeployOptions.IgnoreLockHintsOnIndexes = DeployOptions.IgnoreLockHintsOnIndexes.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreLoginSids != null)
                profile.DeployOptions.IgnoreLoginSids = DeployOptions.IgnoreLoginSids.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreNotForReplication != null)
                profile.DeployOptions.IgnoreNotForReplication = DeployOptions.IgnoreNotForReplication.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreObjectPlacementOnPartitionScheme != null)
                profile.DeployOptions.IgnoreObjectPlacementOnPartitionScheme = DeployOptions.IgnoreObjectPlacementOnPartitionScheme.Value.Expand<bool>(context);
            if (DeployOptions.IgnorePartitionSchemes != null)
                profile.DeployOptions.IgnorePartitionSchemes = DeployOptions.IgnorePartitionSchemes.Value.Expand<bool>(context);
            if (DeployOptions.IgnorePermissions != null)
                profile.DeployOptions.IgnorePermissions = DeployOptions.IgnorePermissions.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreQuotedIdentifiers != null)
                profile.DeployOptions.IgnoreQuotedIdentifiers = DeployOptions.IgnoreQuotedIdentifiers.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreRoleMembership != null)
                profile.DeployOptions.IgnoreRoleMembership = DeployOptions.IgnoreRoleMembership.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreRouteLifetime != null)
                profile.DeployOptions.IgnoreRouteLifetime = DeployOptions.IgnoreRouteLifetime.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreSemicolonBetweenStatements != null)
                profile.DeployOptions.IgnoreSemicolonBetweenStatements = DeployOptions.IgnoreSemicolonBetweenStatements.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreTableOptions != null)
                profile.DeployOptions.IgnoreTableOptions = DeployOptions.IgnoreTableOptions.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreUserSettingsObjects != null)
                profile.DeployOptions.IgnoreUserSettingsObjects = DeployOptions.IgnoreUserSettingsObjects.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreWhitespace != null)
                profile.DeployOptions.IgnoreWhitespace = DeployOptions.IgnoreWhitespace.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreWithNocheckOnCheckConstraints != null)
                profile.DeployOptions.IgnoreWithNocheckOnCheckConstraints = DeployOptions.IgnoreWithNocheckOnCheckConstraints.Value.Expand<bool>(context);
            if (DeployOptions.IgnoreWithNocheckOnForeignKeys != null)
                profile.DeployOptions.IgnoreWithNocheckOnForeignKeys = DeployOptions.IgnoreWithNocheckOnForeignKeys.Value.Expand<bool>(context);
            if (DeployOptions.IncludeCompositeObjects != null)
                profile.DeployOptions.IncludeCompositeObjects = DeployOptions.IncludeCompositeObjects.Value.Expand<bool>(context);
            if (DeployOptions.IncludeTransactionalScripts != null)
                profile.DeployOptions.IncludeTransactionalScripts = DeployOptions.IncludeTransactionalScripts.Value.Expand<bool>(context);
            if (DeployOptions.LongRunningCommandTimeout != null)
                profile.DeployOptions.LongRunningCommandTimeout = DeployOptions.LongRunningCommandTimeout.Value.Expand<int>(context);
            if (DeployOptions.NoAlterStatementsToChangeClrTypes != null)
                profile.DeployOptions.NoAlterStatementsToChangeClrTypes = DeployOptions.NoAlterStatementsToChangeClrTypes.Value.Expand<bool>(context);
            if (DeployOptions.PopulateFilesOnFileGroups != null)
                profile.DeployOptions.PopulateFilesOnFileGroups = DeployOptions.PopulateFilesOnFileGroups.Value.Expand<bool>(context);
            if (DeployOptions.RegisterDataTierApplication != null)
                profile.DeployOptions.RegisterDataTierApplication = DeployOptions.RegisterDataTierApplication.Value.Expand<bool>(context);
            if (DeployOptions.RunDeploymentPlanExecutors != null)
                profile.DeployOptions.RunDeploymentPlanExecutors = DeployOptions.RunDeploymentPlanExecutors.Value.Expand<bool>(context);
            if (DeployOptions.ScriptDatabaseCollation != null)
                profile.DeployOptions.ScriptDatabaseCollation = DeployOptions.ScriptDatabaseCollation.Value.Expand<bool>(context);
            if (DeployOptions.ScriptDatabaseCompatibility != null)
                profile.DeployOptions.ScriptDatabaseCompatibility = DeployOptions.ScriptDatabaseCompatibility.Value.Expand<bool>(context);
            if (DeployOptions.ScriptDatabaseOptions != null)
                profile.DeployOptions.ScriptDatabaseOptions = DeployOptions.ScriptDatabaseOptions.Value.Expand<bool>(context);
            if (DeployOptions.ScriptDeployStateChecks != null)
                profile.DeployOptions.ScriptDeployStateChecks = DeployOptions.ScriptDeployStateChecks.Value.Expand<bool>(context);
            if (DeployOptions.ScriptFileSize != null)
                profile.DeployOptions.ScriptFileSize = DeployOptions.ScriptFileSize.Value.Expand<bool>(context);
            if (DeployOptions.ScriptNewConstraintValidation != null)
                profile.DeployOptions.ScriptNewConstraintValidation = DeployOptions.ScriptNewConstraintValidation.Value.Expand<bool>(context);
            if (DeployOptions.ScriptRefreshModule != null)
                profile.DeployOptions.ScriptRefreshModule = DeployOptions.ScriptRefreshModule.Value.Expand<bool>(context);
            foreach (var kvp in DeployOptions.SqlCommandVariableValues)
                profile.DeployOptions.SqlCommandVariableValues[kvp.Key] = kvp.Value.Expand<string>(context);
            if (DeployOptions.TreatVerificationErrorsAsWarnings != null)
                profile.DeployOptions.TreatVerificationErrorsAsWarnings = DeployOptions.TreatVerificationErrorsAsWarnings.Value.Expand<bool>(context);
            if (DeployOptions.UnmodifiableObjectWarnings != null)
                profile.DeployOptions.UnmodifiableObjectWarnings = DeployOptions.UnmodifiableObjectWarnings.Value.Expand<bool>(context);
            if (DeployOptions.VerifyCollationCompatibility != null)
                profile.DeployOptions.VerifyCollationCompatibility = DeployOptions.VerifyCollationCompatibility.Value.Expand<bool>(context);
            if (DeployOptions.VerifyDeployment != null)
                profile.DeployOptions.VerifyDeployment = DeployOptions.VerifyDeployment.Value.Expand<bool>(context);

            return profile;
        }

        /// <summary>
        /// Extracts an <see cref="ObjectType"/> from a string value.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        ObjectType ToObjectType(string value)
        {
            return (ObjectType)Enum.Parse(typeof(ObjectType), value);
        }

    }

}
