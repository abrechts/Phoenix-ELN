/* 
-------------------------------------------------------------------------------------------
This script creates a Phoenix ELN server database for MariaDB or MySQL. 
-------------------------------------------------------------------------------------------

Create a login User
'-------------------'
In your database tool, please manually create a new database user account for Phoenix ELN.
* The username is REQUIRED to be: EspressoUser
* The passoword is of your own choice. 
This user must be granted at least following privileges: SELECT, ALTER, CREATE, INSERT, DELETE, UPDATE, SUPER.
*/

SET foreign_key_checks = 0;     -- is reset at end of script

CREATE DATABASE IF NOT EXISTS `PhoenixElnData` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci */;
USE `PhoenixElnData`;

CREATE TABLE IF NOT EXISTS `sync_Tombstone` (
  `GUID` varchar(36) NOT NULL,
  `DatabaseInfoID` varchar(36) NOT NULL,
  `TableName` varchar(50) NOT NULL,
  `PrimaryKeyVal` varchar(50) NOT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  KEY `IX_sync_Tombstone_DatabaseInfoID` (`DatabaseInfoID`),
  CONSTRAINT `FK_sync_Tombstone_tblDatabaseInfo_DatabaseInfoID` FOREIGN KEY (`DatabaseInfoID`) REFERENCES `tblDatabaseInfo` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblAuxiliaries` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `SpecifiedUnitType` bigint(20) NOT NULL,
  `IsDisplayAsVolume` tinyint(4) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Source` varchar(40) DEFAULT NULL,
  `Grams` double NOT NULL,
  `Equivalents` double NOT NULL,
  `Density` double DEFAULT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblAuxiliaries` (`ProtocolItemID`),
  CONSTRAINT `FK_tblAuxiliaries_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblComments` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `CommentFlowDoc` mediumtext DEFAULT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblComments` (`ProtocolItemID`),
  CONSTRAINT `FK_tblComments_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblDatabaseInfo` (
  `GUID` varchar(36) NOT NULL,
  `CurrAppVersion` varchar(16) DEFAULT NULL,
  `CompanyLogo` longblob DEFAULT NULL,
  `LastSyncID` varchar(36) DEFAULT NULL,
  `LastSyncTime` varchar(30) DEFAULT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblEmbeddedFiles` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `FileType` bigint(20) NOT NULL,
  `FileName` varchar(50) NOT NULL,
  `FileBytes` longblob NOT NULL,
  `FileSizeMB` double DEFAULT NULL,
  `FileComment` longtext NOT NULL,
  `SHA256Hash` varchar(64) DEFAULT NULL,
  `IconImage` longblob DEFAULT NULL,
  `IsPortraitMode` tinyint(4) DEFAULT 0,
  `IsRotated` tinyint(4) DEFAULT 0,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `idx_tblEmbeddedFiles` (`ProtocolItemID`),
  CONSTRAINT `FK_tblEmbeddedFiles_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblExperiments` (
  `ExperimentID` varchar(25) NOT NULL,
  `UserID` varchar(25) NOT NULL,
  `ProjectID` varchar(36) NOT NULL,
  `IsDesignView` tinyint(4) NOT NULL,
  `IsCurrent` tinyint(4) NOT NULL,
  `RxnSketch` longtext DEFAULT NULL,
  `MDLRxnFileString` longtext DEFAULT NULL,
  `ReactantInChIKey` varchar(27) DEFAULT NULL,
  `ProductInChIKey` varchar(27) DEFAULT NULL,
  `WorkflowState` smallint(6) DEFAULT 0,
  `UserTag` smallint(6) DEFAULT 0,
  `Yield` double DEFAULT NULL,
  `Purity` double DEFAULT NULL,
  `RefReactantGrams` double DEFAULT NULL,
  `RefReactantMMols` double DEFAULT NULL,
  `RefYieldFactor` double NOT NULL DEFAULT 1,
  `CreationDate` varchar(20) DEFAULT NULL,
  `FinalizeDate` varchar(20) DEFAULT NULL,
  `DisplayIndex` smallint(6) DEFAULT NULL,
  `IsNodeExpanded` tinyint(4) DEFAULT 0,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`ExperimentID`),
  KEY `IX_tblExperiments_ProjectID` (`ProjectID`),
  KEY `IX_tblExperiments_UserID` (`UserID`),
  CONSTRAINT `FK_tblExperiments_tblProjects_ProjectID` FOREIGN KEY (`ProjectID`) REFERENCES `tblProjects` (`GUID`) ON DELETE CASCADE,
  CONSTRAINT `FK_tblExperiments_tblUsers_UserID` FOREIGN KEY (`UserID`) REFERENCES `tblUsers` (`UserID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblMaterials` (
  `GUID` varchar(36) NOT NULL,
  `DatabaseID` varchar(36) NOT NULL,
  `MatName` varchar(150) NOT NULL,
  `MatSource` varchar(100) DEFAULT NULL,
  `MatType` smallint(6) NOT NULL,
  `Molweight` double DEFAULT NULL,
  `Density` double DEFAULT NULL,
  `Purity` double DEFAULT NULL,
  `Molarity` double DEFAULT NULL,
  `InChIKey` varchar(30) DEFAULT NULL,
  `IsValidated` tinyint(4) DEFAULT 0,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  KEY `IX_tblMaterials_DatabaseID` (`DatabaseID`),
  CONSTRAINT `FK_tblMaterials_tblDatabaseInfo_DatabaseID` FOREIGN KEY (`DatabaseID`) REFERENCES `tblDatabaseInfo` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblProducts` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `ProductIndex` smallint(6) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Grams` double NOT NULL,
  `MolecularWeight` double NOT NULL,
  `Yield` double NOT NULL,
  `ExactMass` double DEFAULT NULL,
  `ElementalFormula` varchar(50) DEFAULT NULL,
  `Purity` double DEFAULT NULL,
  `ResinLoad` double DEFAULT NULL,
  `BatchID` varchar(40) DEFAULT NULL,
  `InChIKey` varchar(27) DEFAULT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblProducts_ProtocolItemID` (`ProtocolItemID`),
  CONSTRAINT `FK_tblProducts_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblProjects` (
  `GUID` varchar(36) NOT NULL,
  `UserID` varchar(25) NOT NULL,
  `Title` varchar(100) DEFAULT NULL,
  `SequenceNr` bigint(20) DEFAULT NULL,
  `IsNodeExpanded` tinyint(4) NOT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `pk_tblProjects_2` (`GUID`),
  KEY `IX_tblProjects_UserID` (`UserID`),
  CONSTRAINT `FK_tblProjects_tblUsers_UserID` FOREIGN KEY (`UserID`) REFERENCES `tblUsers` (`UserID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblProtocolItems` (
  `GUID` varchar(36) NOT NULL,
  `ExperimentID` varchar(25) NOT NULL,
  `ElementType` smallint(6) NOT NULL,
  `SequenceNr` smallint(6) DEFAULT NULL,
  `TempInfo` longtext DEFAULT NULL,
  `IsSelected` tinyint(4) NOT NULL DEFAULT 0,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  KEY `idx_tblProtocolItems` (`ExperimentID`),
  CONSTRAINT `FK_tblProtocolItems_tblExperiments_ExperimentID` FOREIGN KEY (`ExperimentID`) REFERENCES `tblExperiments` (`ExperimentID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblReagents` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `SpecifiedUnitType` bigint(20) NOT NULL,
  `IsDisplayAsVolume` tinyint(4) NOT NULL,
  `MolecularWeight` double DEFAULT NULL,
  `Name` varchar(50) NOT NULL,
  `Source` varchar(50) DEFAULT NULL,
  `Grams` double NOT NULL,
  `MMols` double NOT NULL,
  `Equivalents` double NOT NULL,
  `Density` double DEFAULT NULL,
  `Purity` double DEFAULT NULL,
  `ResinLoad` double DEFAULT NULL,
  `Molarity` double DEFAULT NULL,
  `IsMolarity` tinyint(4) NOT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblReagents_ProtocolItemID` (`ProtocolItemID`),
  CONSTRAINT `FK_tblReagents_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblRefReactants` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `SpecifiedUnitType` bigint(20) NOT NULL,
  `IsDisplayAsVolume` tinyint(4) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Source` varchar(40) DEFAULT NULL,
  `MolecularWeight` double NOT NULL,
  `Grams` double NOT NULL,
  `MMols` double NOT NULL,
  `Equivalents` double NOT NULL,
  `Density` double DEFAULT NULL,
  `Purity` double DEFAULT NULL,
  `ResinLoad` double DEFAULT NULL,
  `InChIKey` varchar(27) DEFAULT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblRefReactants` (`ProtocolItemID`),
  CONSTRAINT `FK_tblRefReactants_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblSeparators` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `ElementType` smallint(6) NOT NULL DEFAULT 0,
  `DisplayType` smallint(6) NOT NULL DEFAULT 0,
  `Title` varchar(80) DEFAULT NULL,
  `SyncState` tinyint(4) NOT NULL DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `unq_tblSeparators` (`ProtocolItemID`),
  CONSTRAINT `FK_tblSeparators_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblSolvents` (
  `GUID` varchar(36) NOT NULL,
  `ProtocolItemID` varchar(36) NOT NULL,
  `SpecifiedUnitType` bigint(20) NOT NULL DEFAULT 1,
  `IsDisplayAsWeight` tinyint(4) NOT NULL,
  `Name` varchar(50) NOT NULL,
  `Source` varchar(40) DEFAULT NULL,
  `Milliliters` double NOT NULL,
  `Density` double DEFAULT NULL,
  `IsMolEquivalents` tinyint(4) NOT NULL,
  `Equivalents` double NOT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`GUID`),
  UNIQUE KEY `idx_tblSolvents` (`ProtocolItemID`),
  CONSTRAINT `FK_tblSolvents_tblProtocolItems_ProtocolItemID` FOREIGN KEY (`ProtocolItemID`) REFERENCES `tblProtocolItems` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;


CREATE TABLE IF NOT EXISTS `tblUsers` (
  `UserID` varchar(25) NOT NULL,
  `DatabaseID` varchar(36) NOT NULL,
  `FirstName` varchar(50) DEFAULT NULL,
  `LastName` varchar(50) DEFAULT NULL,
  `CompanyName` varchar(100) DEFAULT NULL,
  `DepartmentName` varchar(100) DEFAULT NULL,
  `City` varchar(50) DEFAULT NULL,
  `PWHash` varchar(64) DEFAULT NULL,
  `PWHint` varchar(80) DEFAULT NULL,
  `IsSpellCheckEnabled` tinyint(4) NOT NULL,
  `SyncState` tinyint(4) DEFAULT 0,
  PRIMARY KEY (`UserID`),
  KEY `IX_tblUsers_DatabaseID` (`DatabaseID`),
  CONSTRAINT `FK_tblUsers_tblDatabaseInfo_DatabaseID` FOREIGN KEY (`DatabaseID`) REFERENCES `tblDatabaseInfo` (`GUID`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

SET foreign_key_checks = 1;

