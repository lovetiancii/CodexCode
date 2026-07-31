-- Tianci OA / MySQL 8.0
-- All IDs are application-generated Snowflake IDs. AUTO_INCREMENT is forbidden.
CREATE DATABASE IF NOT EXISTS `tianci_oa`
  CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE `tianci_oa`;

SET NAMES utf8mb4;
SET time_zone = '+00:00';

CREATE TABLE IF NOT EXISTS `department` (
  `id` bigint unsigned NOT NULL COMMENT 'Snowflake ID',
  `parent_id` bigint unsigned NULL,
  `name` varchar(100) NOT NULL,
  `code` varchar(64) NOT NULL,
  `leader_employee_id` bigint unsigned NULL COMMENT 'Validated by application; FK added after employee exists',
  `sort_order` int NOT NULL DEFAULT 0,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '0 disabled, 1 enabled',
  `remark` varchar(500) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_department_code` (`code`),
  KEY `idx_department_parent_sort` (`parent_id`, `sort_order`),
  KEY `idx_department_status_deleted` (`status`, `is_deleted`),
  CONSTRAINT `fk_department_parent` FOREIGN KEY (`parent_id`) REFERENCES `department` (`id`),
  CONSTRAINT `chk_department_status` CHECK (`status` IN (0,1)),
  CONSTRAINT `chk_department_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Department tree';

CREATE TABLE IF NOT EXISTS `position` (
  `id` bigint unsigned NOT NULL,
  `department_id` bigint unsigned NOT NULL,
  `name` varchar(100) NOT NULL,
  `code` varchar(64) NOT NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '0 disabled, 1 enabled',
  `remark` varchar(500) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_position_code` (`code`),
  UNIQUE KEY `uk_position_department_name` (`department_id`, `name`),
  KEY `idx_position_department_status` (`department_id`, `status`, `is_deleted`),
  CONSTRAINT `fk_position_department` FOREIGN KEY (`department_id`) REFERENCES `department` (`id`),
  CONSTRAINT `chk_position_status` CHECK (`status` IN (0,1)),
  CONSTRAINT `chk_position_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Position';

CREATE TABLE IF NOT EXISTS `sys_user` (
  `id` bigint unsigned NOT NULL,
  `username` varchar(64) NOT NULL,
  `display_name` varchar(100) NOT NULL,
  `password_hash` varchar(500) NULL COMMENT 'ASP.NET PasswordHasher result; NULL cannot authenticate',
  `phone` varchar(32) NULL,
  `email` varchar(254) NULL,
  `employee_id` bigint unsigned NULL COMMENT 'FK added after employee exists',
  `department_id` bigint unsigned NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 0 COMMENT '0 disabled, 1 enabled, 2 locked',
  `requires_initialization` tinyint(1) NOT NULL DEFAULT 1,
  `security_stamp` varchar(64) NOT NULL,
  `last_login_at` datetime(3) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_user_username` (`username`),
  UNIQUE KEY `uk_sys_user_employee` (`employee_id`),
  KEY `idx_sys_user_department_status` (`department_id`, `status`, `is_deleted`),
  KEY `idx_sys_user_phone` (`phone`),
  KEY `idx_sys_user_email` (`email`),
  CONSTRAINT `fk_sys_user_department` FOREIGN KEY (`department_id`) REFERENCES `department` (`id`),
  CONSTRAINT `chk_sys_user_status` CHECK (`status` IN (0,1,2)),
  CONSTRAINT `chk_sys_user_init` CHECK (`requires_initialization` IN (0,1)),
  CONSTRAINT `chk_sys_user_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='System user';

CREATE TABLE IF NOT EXISTS `sys_role` (
  `id` bigint unsigned NOT NULL,
  `name` varchar(100) NOT NULL,
  `code` varchar(64) NOT NULL,
  `data_scope` tinyint unsigned NOT NULL DEFAULT 3 COMMENT '1 all, 2 department and children, 3 self',
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '0 disabled, 1 enabled',
  `is_system` tinyint(1) NOT NULL DEFAULT 0,
  `remark` varchar(500) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_role_code` (`code`),
  KEY `idx_sys_role_status_deleted` (`status`, `is_deleted`),
  CONSTRAINT `chk_sys_role_scope` CHECK (`data_scope` IN (1,2,3)),
  CONSTRAINT `chk_sys_role_status` CHECK (`status` IN (0,1)),
  CONSTRAINT `chk_sys_role_system` CHECK (`is_system` IN (0,1)),
  CONSTRAINT `chk_sys_role_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='RBAC role';

CREATE TABLE IF NOT EXISTS `sys_menu` (
  `id` bigint unsigned NOT NULL,
  `parent_id` bigint unsigned NULL,
  `type` tinyint unsigned NOT NULL COMMENT '1 directory, 2 menu, 3 action',
  `name` varchar(100) NOT NULL,
  `route_path` varchar(255) NULL,
  `component` varchar(255) NULL,
  `permission_code` varchar(128) NULL,
  `icon` varchar(64) NULL,
  `sort_order` int NOT NULL DEFAULT 0,
  `visible` tinyint(1) NOT NULL DEFAULT 1,
  `status` tinyint unsigned NOT NULL DEFAULT 1,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_menu_permission` (`permission_code`),
  KEY `idx_sys_menu_parent_sort` (`parent_id`, `sort_order`),
  KEY `idx_sys_menu_type_status` (`type`, `status`, `is_deleted`),
  CONSTRAINT `fk_sys_menu_parent` FOREIGN KEY (`parent_id`) REFERENCES `sys_menu` (`id`),
  CONSTRAINT `chk_sys_menu_type` CHECK (`type` IN (1,2,3)),
  CONSTRAINT `chk_sys_menu_visible` CHECK (`visible` IN (0,1)),
  CONSTRAINT `chk_sys_menu_status` CHECK (`status` IN (0,1)),
  CONSTRAINT `chk_sys_menu_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Menu and action permission';

CREATE TABLE IF NOT EXISTS `sys_user_role` (
  `id` bigint unsigned NOT NULL,
  `user_id` bigint unsigned NOT NULL,
  `role_id` bigint unsigned NOT NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_user_role` (`user_id`, `role_id`),
  KEY `idx_sys_user_role_role` (`role_id`, `user_id`),
  CONSTRAINT `fk_sys_user_role_user` FOREIGN KEY (`user_id`) REFERENCES `sys_user` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_sys_user_role_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='User-role relation';

CREATE TABLE IF NOT EXISTS `sys_role_menu` (
  `id` bigint unsigned NOT NULL,
  `role_id` bigint unsigned NOT NULL,
  `menu_id` bigint unsigned NOT NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_role_menu` (`role_id`, `menu_id`),
  KEY `idx_sys_role_menu_menu` (`menu_id`, `role_id`),
  CONSTRAINT `fk_sys_role_menu_role` FOREIGN KEY (`role_id`) REFERENCES `sys_role` (`id`) ON DELETE CASCADE,
  CONSTRAINT `fk_sys_role_menu_menu` FOREIGN KEY (`menu_id`) REFERENCES `sys_menu` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB COMMENT='Role-menu relation';

CREATE TABLE IF NOT EXISTS `sys_file` (
  `id` bigint unsigned NOT NULL,
  `business_type` varchar(64) NOT NULL COMMENT 'resume/entry/contract/etc.',
  `business_id` bigint unsigned NOT NULL,
  `category` varchar(64) NOT NULL,
  `original_name` varchar(255) NOT NULL,
  `storage_provider` varchar(32) NOT NULL DEFAULT 'local',
  `storage_key` varchar(500) NOT NULL,
  `content_type` varchar(128) NOT NULL,
  `extension` varchar(16) NOT NULL,
  `size_bytes` bigint unsigned NOT NULL,
  `sha256` char(64) NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '0 temporary, 1 active, 2 quarantined',
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_sys_file_storage_key` (`storage_key`),
  KEY `idx_sys_file_business` (`business_type`, `business_id`, `category`, `is_deleted`),
  KEY `idx_sys_file_status_created` (`status`, `created_at`),
  CONSTRAINT `chk_sys_file_status` CHECK (`status` IN (0,1,2)),
  CONSTRAINT `chk_sys_file_deleted` CHECK (`is_deleted` IN (0,1)),
  CONSTRAINT `chk_sys_file_size` CHECK (`size_bytes` > 0)
) ENGINE=InnoDB COMMENT='Controlled business attachment';

CREATE TABLE IF NOT EXISTS `resume` (
  `id` bigint unsigned NOT NULL,
  `candidate_no` varchar(32) NOT NULL,
  `name` varchar(100) NOT NULL,
  `gender` tinyint unsigned NOT NULL DEFAULT 0 COMMENT '0 unknown, 1 male, 2 female',
  `phone` varchar(32) NOT NULL,
  `email` varchar(254) NULL,
  `education` varchar(64) NULL,
  `work_experience` text NULL,
  `skills` text NULL,
  `applied_position_id` bigint unsigned NOT NULL,
  `source` varchar(64) NULL,
  `attachment_file_id` bigint unsigned NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 submitted,2 screening,3 interview_pending,4 interviewing,5 offer_pending,6 entry_pending,7 hired,8 rejected,9 offer_declined',
  `current_round` tinyint unsigned NOT NULL DEFAULT 0,
  `owner_user_id` bigint unsigned NULL,
  `reject_reason` varchar(500) NULL,
  `remark` varchar(1000) NULL,
  `version` int unsigned NOT NULL DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_resume_candidate_no` (`candidate_no`),
  KEY `idx_resume_status_position_created` (`status`, `applied_position_id`, `created_at`),
  KEY `idx_resume_phone` (`phone`),
  KEY `idx_resume_email` (`email`),
  KEY `idx_resume_owner_status` (`owner_user_id`, `status`),
  CONSTRAINT `fk_resume_position` FOREIGN KEY (`applied_position_id`) REFERENCES `position` (`id`),
  CONSTRAINT `fk_resume_attachment` FOREIGN KEY (`attachment_file_id`) REFERENCES `sys_file` (`id`),
  CONSTRAINT `fk_resume_owner` FOREIGN KEY (`owner_user_id`) REFERENCES `sys_user` (`id`),
  CONSTRAINT `chk_resume_gender` CHECK (`gender` IN (0,1,2)),
  CONSTRAINT `chk_resume_status` CHECK (`status` BETWEEN 1 AND 9),
  CONSTRAINT `chk_resume_round` CHECK (`current_round` BETWEEN 0 AND 5),
  CONSTRAINT `chk_resume_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Candidate resume';

CREATE TABLE IF NOT EXISTS `interview_record` (
  `id` bigint unsigned NOT NULL,
  `resume_id` bigint unsigned NOT NULL,
  `round_no` tinyint unsigned NOT NULL,
  `interviewer_user_id` bigint unsigned NOT NULL,
  `scheduled_at` datetime(3) NOT NULL,
  `location` varchar(255) NULL,
  `score` decimal(5,2) NULL,
  `evaluation` varchar(2000) NULL,
  `conclusion` tinyint unsigned NOT NULL DEFAULT 0 COMMENT '0 pending, 1 pass, 2 fail, 3 hold, 4 cancelled',
  `next_scheduled_at` datetime(3) NULL,
  `completed_at` datetime(3) NULL,
  `remark` varchar(1000) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_interview_resume_round` (`resume_id`, `round_no`),
  KEY `idx_interview_interviewer_time` (`interviewer_user_id`, `scheduled_at`),
  KEY `idx_interview_conclusion_time` (`conclusion`, `scheduled_at`),
  CONSTRAINT `fk_interview_resume` FOREIGN KEY (`resume_id`) REFERENCES `resume` (`id`),
  CONSTRAINT `fk_interview_user` FOREIGN KEY (`interviewer_user_id`) REFERENCES `sys_user` (`id`),
  CONSTRAINT `chk_interview_round` CHECK (`round_no` BETWEEN 1 AND 5),
  CONSTRAINT `chk_interview_score` CHECK (`score` IS NULL OR (`score` >= 0 AND `score` <= 100)),
  CONSTRAINT `chk_interview_conclusion` CHECK (`conclusion` IN (0,1,2,3,4)),
  CONSTRAINT `chk_interview_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Multi-round interview record';

CREATE TABLE IF NOT EXISTS `employee` (
  `id` bigint unsigned NOT NULL,
  `employee_no` varchar(32) NOT NULL,
  `source_resume_id` bigint unsigned NULL,
  `name` varchar(100) NOT NULL,
  `gender` tinyint unsigned NOT NULL DEFAULT 0,
  `phone` varchar(32) NOT NULL,
  `email` varchar(254) NULL,
  `id_card_ciphertext` varchar(512) NULL COMMENT 'Encrypted; never plaintext',
  `department_id` bigint unsigned NOT NULL,
  `position_id` bigint unsigned NOT NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 probation,2 active,3 terminated,4 archived',
  `entry_date` date NOT NULL,
  `probation_months` tinyint unsigned NOT NULL DEFAULT 0,
  `regular_date` date NULL,
  `monthly_salary_ciphertext` varchar(512) NULL COMMENT 'Encrypted; never plaintext',
  `termination_date` date NULL,
  `termination_reason` varchar(500) NULL,
  `version` int unsigned NOT NULL DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_employee_no` (`employee_no`),
  UNIQUE KEY `uk_employee_resume` (`source_resume_id`),
  KEY `idx_employee_department_status` (`department_id`, `status`, `is_deleted`),
  KEY `idx_employee_position_status` (`position_id`, `status`, `is_deleted`),
  KEY `idx_employee_name` (`name`),
  KEY `idx_employee_phone` (`phone`),
  CONSTRAINT `fk_employee_resume` FOREIGN KEY (`source_resume_id`) REFERENCES `resume` (`id`),
  CONSTRAINT `fk_employee_department` FOREIGN KEY (`department_id`) REFERENCES `department` (`id`),
  CONSTRAINT `fk_employee_position` FOREIGN KEY (`position_id`) REFERENCES `position` (`id`),
  CONSTRAINT `chk_employee_gender` CHECK (`gender` IN (0,1,2)),
  CONSTRAINT `chk_employee_status` CHECK (`status` IN (1,2,3,4)),
  CONSTRAINT `chk_employee_probation` CHECK (`probation_months` BETWEEN 0 AND 12),
  CONSTRAINT `chk_employee_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Employee master record';

CREATE TABLE IF NOT EXISTS `employee_entry` (
  `id` bigint unsigned NOT NULL,
  `resume_id` bigint unsigned NOT NULL,
  `employee_id` bigint unsigned NULL,
  `planned_entry_date` date NOT NULL,
  `actual_entry_date` date NULL,
  `department_id` bigint unsigned NOT NULL,
  `position_id` bigint unsigned NOT NULL,
  `monthly_salary_ciphertext` varchar(512) NULL,
  `probation_months` tinyint unsigned NOT NULL DEFAULT 3,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 offer_confirmed,2 entry_pending,3 entered,4 declined,5 cancelled',
  `decline_reason` varchar(500) NULL,
  `remark` varchar(1000) NULL,
  `version` int unsigned NOT NULL DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_entry_resume` (`resume_id`),
  UNIQUE KEY `uk_entry_employee` (`employee_id`),
  KEY `idx_entry_status_planned` (`status`, `planned_entry_date`),
  KEY `idx_entry_department_status` (`department_id`, `status`),
  CONSTRAINT `fk_entry_resume` FOREIGN KEY (`resume_id`) REFERENCES `resume` (`id`),
  CONSTRAINT `fk_entry_employee` FOREIGN KEY (`employee_id`) REFERENCES `employee` (`id`),
  CONSTRAINT `fk_entry_department` FOREIGN KEY (`department_id`) REFERENCES `department` (`id`),
  CONSTRAINT `fk_entry_position` FOREIGN KEY (`position_id`) REFERENCES `position` (`id`),
  CONSTRAINT `chk_entry_status` CHECK (`status` IN (1,2,3,4,5)),
  CONSTRAINT `chk_entry_probation` CHECK (`probation_months` BETWEEN 0 AND 12),
  CONSTRAINT `chk_entry_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Employee entry process';

CREATE TABLE IF NOT EXISTS `employee_contract` (
  `id` bigint unsigned NOT NULL,
  `contract_no` varchar(64) NOT NULL,
  `employee_id` bigint unsigned NOT NULL,
  `contract_type` tinyint unsigned NOT NULL COMMENT '1 labor,2 internship,3 confidentiality,4 other',
  `start_date` date NOT NULL,
  `end_date` date NOT NULL,
  `reminder_days` smallint unsigned NOT NULL DEFAULT 30,
  `attachment_file_id` bigint unsigned NULL,
  `previous_contract_id` bigint unsigned NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 draft,2 active,3 terminated,4 renewed,5 archived',
  `terminated_at` datetime(3) NULL,
  `remark` varchar(1000) NULL,
  `version` int unsigned NOT NULL DEFAULT 0,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_contract_no` (`contract_no`),
  KEY `idx_contract_employee_status` (`employee_id`, `status`, `is_deleted`),
  KEY `idx_contract_status_end` (`status`, `end_date`),
  CONSTRAINT `fk_contract_employee` FOREIGN KEY (`employee_id`) REFERENCES `employee` (`id`),
  CONSTRAINT `fk_contract_attachment` FOREIGN KEY (`attachment_file_id`) REFERENCES `sys_file` (`id`),
  CONSTRAINT `fk_contract_previous` FOREIGN KEY (`previous_contract_id`) REFERENCES `employee_contract` (`id`),
  CONSTRAINT `chk_contract_type` CHECK (`contract_type` IN (1,2,3,4)),
  CONSTRAINT `chk_contract_status` CHECK (`status` IN (1,2,3,4,5)),
  CONSTRAINT `chk_contract_dates` CHECK (`start_date` <= `end_date`),
  CONSTRAINT `chk_contract_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Employee contract';

CREATE TABLE IF NOT EXISTS `operation_log` (
  `id` bigint unsigned NOT NULL,
  `trace_id` varchar(64) NULL,
  `operator_user_id` bigint unsigned NULL,
  `operator_name` varchar(100) NULL,
  `module` varchar(64) NOT NULL,
  `action` varchar(64) NOT NULL,
  `business_type` varchar(64) NULL,
  `business_id` bigint unsigned NULL,
  `request_method` varchar(16) NULL,
  `request_path` varchar(500) NULL,
  `client_ip` varchar(64) NULL,
  `result` tinyint unsigned NOT NULL COMMENT '0 failed, 1 succeeded',
  `before_status` varchar(64) NULL,
  `after_status` varchar(64) NULL,
  `change_summary` json NULL COMMENT 'Must be desensitized',
  `error_code` varchar(64) NULL,
  `duration_ms` int unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (`id`),
  KEY `idx_operation_operator_time` (`operator_user_id`, `created_at`),
  KEY `idx_operation_business_time` (`business_type`, `business_id`, `created_at`),
  KEY `idx_operation_module_action_time` (`module`, `action`, `created_at`),
  KEY `idx_operation_trace` (`trace_id`),
  CONSTRAINT `fk_operation_user` FOREIGN KEY (`operator_user_id`) REFERENCES `sys_user` (`id`),
  CONSTRAINT `chk_operation_result` CHECK (`result` IN (0,1))
) ENGINE=InnoDB COMMENT='Append-only operation audit log';

CREATE TABLE IF NOT EXISTS `workflow_instance` (
  `id` bigint unsigned NOT NULL,
  `workflow_type` varchar(64) NOT NULL,
  `business_type` varchar(64) NOT NULL,
  `business_id` bigint unsigned NOT NULL,
  `current_node_code` varchar(64) NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 running,2 completed,3 rejected,4 cancelled',
  `version` int unsigned NOT NULL DEFAULT 0,
  `started_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `completed_at` datetime(3) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_workflow_business` (`workflow_type`, `business_type`, `business_id`),
  KEY `idx_workflow_status_updated` (`status`, `updated_at`),
  CONSTRAINT `chk_workflow_instance_status` CHECK (`status` IN (1,2,3,4)),
  CONSTRAINT `chk_workflow_instance_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Workflow instance';

CREATE TABLE IF NOT EXISTS `workflow_node` (
  `id` bigint unsigned NOT NULL,
  `instance_id` bigint unsigned NOT NULL,
  `node_code` varchar(64) NOT NULL,
  `node_name` varchar(100) NOT NULL,
  `sequence_no` int unsigned NOT NULL,
  `approval_mode` tinyint unsigned NOT NULL DEFAULT 1 COMMENT '1 single,2 any,3 all',
  `assignee_user_id` bigint unsigned NULL,
  `status` tinyint unsigned NOT NULL DEFAULT 0 COMMENT '0 pending,1 active,2 passed,3 rejected,4 skipped,5 cancelled',
  `started_at` datetime(3) NULL,
  `completed_at` datetime(3) NULL,
  `is_deleted` tinyint(1) NOT NULL DEFAULT 0,
  `deleted_at` datetime(3) NULL,
  `deleted_by` bigint unsigned NULL,
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  `updated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
  `updated_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_workflow_node_code` (`instance_id`, `node_code`),
  UNIQUE KEY `uk_workflow_node_sequence` (`instance_id`, `sequence_no`),
  KEY `idx_workflow_node_assignee` (`assignee_user_id`, `status`),
  CONSTRAINT `fk_workflow_node_instance` FOREIGN KEY (`instance_id`) REFERENCES `workflow_instance` (`id`),
  CONSTRAINT `fk_workflow_node_assignee` FOREIGN KEY (`assignee_user_id`) REFERENCES `sys_user` (`id`),
  CONSTRAINT `chk_workflow_node_mode` CHECK (`approval_mode` IN (1,2,3)),
  CONSTRAINT `chk_workflow_node_status` CHECK (`status` IN (0,1,2,3,4,5)),
  CONSTRAINT `chk_workflow_node_deleted` CHECK (`is_deleted` IN (0,1))
) ENGINE=InnoDB COMMENT='Workflow node snapshot';

CREATE TABLE IF NOT EXISTS `workflow_record` (
  `id` bigint unsigned NOT NULL,
  `instance_id` bigint unsigned NOT NULL,
  `from_node_id` bigint unsigned NULL,
  `to_node_id` bigint unsigned NULL,
  `action` varchar(64) NOT NULL,
  `operator_user_id` bigint unsigned NULL,
  `opinion` varchar(2000) NULL,
  `request_id` varchar(64) NULL COMMENT 'Idempotency key',
  `operated_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_at` datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  `created_by` bigint unsigned NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_workflow_record_request` (`instance_id`, `request_id`),
  KEY `idx_workflow_record_instance_time` (`instance_id`, `operated_at`),
  KEY `idx_workflow_record_operator_time` (`operator_user_id`, `operated_at`),
  CONSTRAINT `fk_workflow_record_instance` FOREIGN KEY (`instance_id`) REFERENCES `workflow_instance` (`id`),
  CONSTRAINT `fk_workflow_record_from` FOREIGN KEY (`from_node_id`) REFERENCES `workflow_node` (`id`),
  CONSTRAINT `fk_workflow_record_to` FOREIGN KEY (`to_node_id`) REFERENCES `workflow_node` (`id`),
  CONSTRAINT `fk_workflow_record_operator` FOREIGN KEY (`operator_user_id`) REFERENCES `sys_user` (`id`)
) ENGINE=InnoDB COMMENT='Append-only workflow action record';

-- Cyclic organizational references are added after both sides exist.
SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `department` ADD CONSTRAINT `fk_department_leader` FOREIGN KEY (`leader_employee_id`) REFERENCES `employee` (`id`)',
    'SELECT 1')
  FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'department' AND CONSTRAINT_NAME = 'fk_department_leader'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @ddl := (
  SELECT IF(COUNT(*) = 0,
    'ALTER TABLE `sys_user` ADD CONSTRAINT `fk_sys_user_employee` FOREIGN KEY (`employee_id`) REFERENCES `employee` (`id`)',
    'SELECT 1')
  FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'sys_user' AND CONSTRAINT_NAME = 'fk_sys_user_employee'
);
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- Stable reserved IDs are seed-only constants, not AUTO_INCREMENT values.
INSERT INTO `sys_role`
(`id`,`name`,`code`,`data_scope`,`status`,`is_system`,`remark`,`created_by`,`updated_by`)
VALUES
(900000000000000001,'超级管理员','SUPER_ADMIN',1,1,1,'系统内置角色，不可删除',NULL,NULL)
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`status`=1,`is_system`=1;

INSERT IGNORE INTO `sys_user`
(`id`,`username`,`display_name`,`password_hash`,`status`,`requires_initialization`,`security_stamp`,`created_by`,`updated_by`)
VALUES
(900000000000000002,'admin','系统管理员',NULL,0,1,'INITIALIZATION_REQUIRED',NULL,NULL);

INSERT INTO `sys_menu`
(`id`,`parent_id`,`type`,`name`,`route_path`,`component`,`permission_code`,`sort_order`,`visible`,`status`)
VALUES
(900000000000000010,NULL,1,'首页',NULL,NULL,NULL,10,1,1),
(900000000000000011,900000000000000010,2,'数据概览','/dashboard','dashboard/index','dashboard:view',10,1,1),
(900000000000000020,NULL,1,'系统管理',NULL,NULL,NULL,90,1,1),
(900000000000000021,900000000000000020,2,'用户管理','/system/users','system/users/index','system:user',10,1,1),
(900000000000000022,900000000000000020,2,'角色管理','/system/roles','system/roles/index','system:role',20,1,1),
(900000000000000023,900000000000000020,2,'菜单管理','/system/menus','system/menus/index','system:menu',30,1,1)
ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`status`=1;

INSERT INTO `sys_user_role` (`id`,`user_id`,`role_id`,`created_by`)
VALUES (900000000000000030,900000000000000002,900000000000000001,NULL)
ON DUPLICATE KEY UPDATE `role_id`=VALUES(`role_id`);

INSERT INTO `sys_role_menu` (`id`,`role_id`,`menu_id`,`created_by`)
VALUES
(900000000000000040,900000000000000001,900000000000000010,NULL),
(900000000000000041,900000000000000001,900000000000000011,NULL),
(900000000000000042,900000000000000001,900000000000000020,NULL),
(900000000000000043,900000000000000001,900000000000000021,NULL),
(900000000000000044,900000000000000001,900000000000000022,NULL),
(900000000000000045,900000000000000001,900000000000000023,NULL)
ON DUPLICATE KEY UPDATE `menu_id`=VALUES(`menu_id`);

-- Security invariant: a newly seeded account cannot log in. INSERT IGNORE ensures
-- rerunning this script never resets an administrator that was securely initialized.
-- Application startup must
-- atomically set a PasswordHasher hash, rotate security_stamp, clear
-- requires_initialization and enable status after a secure one-time setup.
