CREATE TABLE `articles` (
	`id` text PRIMARY KEY NOT NULL,
	`slug` text NOT NULL,
	`title` text NOT NULL,
	`excerpt` text NOT NULL,
	`content` text NOT NULL,
	`category` text NOT NULL,
	`published` integer DEFAULT false NOT NULL,
	`seo_title` text DEFAULT '' NOT NULL,
	`seo_description` text DEFAULT '' NOT NULL,
	`created_at` text NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `articles_slug_unique` ON `articles` (`slug`);--> statement-breakpoint
CREATE TABLE `audit_logs` (
	`id` text PRIMARY KEY NOT NULL,
	`actor` text NOT NULL,
	`action` text NOT NULL,
	`created_at` text NOT NULL
);
--> statement-breakpoint
CREATE TABLE `events` (
	`id` text PRIMARY KEY NOT NULL,
	`order_id` text NOT NULL,
	`status` text NOT NULL,
	`note` text NOT NULL,
	`actor` text NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`order_id`) REFERENCES `orders`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE TABLE `files` (
	`id` text PRIMARY KEY NOT NULL,
	`order_id` text NOT NULL,
	`object_key` text NOT NULL,
	`name` text NOT NULL,
	`size` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`order_id`) REFERENCES `orders`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE UNIQUE INDEX `files_object_key_unique` ON `files` (`object_key`);--> statement-breakpoint
CREATE TABLE `messages` (
	`id` text PRIMARY KEY NOT NULL,
	`name` text NOT NULL,
	`phone` text NOT NULL,
	`subject` text NOT NULL,
	`message` text NOT NULL,
	`read` integer DEFAULT false NOT NULL,
	`created_at` text NOT NULL
);
--> statement-breakpoint
CREATE TABLE `orders` (
	`id` text PRIMARY KEY NOT NULL,
	`code` text NOT NULL,
	`customer` text NOT NULL,
	`phone` text NOT NULL,
	`email` text DEFAULT '' NOT NULL,
	`title` text NOT NULL,
	`service` text NOT NULL,
	`spec` text NOT NULL,
	`estimate` integer NOT NULL,
	`final_price` integer,
	`status` text DEFAULT 'new' NOT NULL,
	`notes` text DEFAULT '' NOT NULL,
	`admin_notes` text DEFAULT '' NOT NULL,
	`version` integer DEFAULT 1 NOT NULL,
	`created_at` text NOT NULL,
	`updated_at` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `orders_code_unique` ON `orders` (`code`);--> statement-breakpoint
CREATE INDEX `orders_created_idx` ON `orders` (`created_at`);--> statement-breakpoint
CREATE INDEX `orders_status_idx` ON `orders` (`status`);--> statement-breakpoint
CREATE INDEX `orders_phone_idx` ON `orders` (`phone`);--> statement-breakpoint
CREATE TABLE `rate_limits` (
	`key` text PRIMARY KEY NOT NULL,
	`count` integer NOT NULL,
	`expires` integer NOT NULL
);
--> statement-breakpoint
CREATE TABLE `settings` (
	`key` text PRIMARY KEY NOT NULL,
	`value` text NOT NULL
);
--> statement-breakpoint
CREATE TABLE `sms_logs` (
	`id` text PRIMARY KEY NOT NULL,
	`event_id` text NOT NULL,
	`order_id` text NOT NULL,
	`phone` text NOT NULL,
	`status` text NOT NULL,
	`receipt` text,
	`error` text,
	`created_at` text NOT NULL,
	FOREIGN KEY (`order_id`) REFERENCES `orders`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE UNIQUE INDEX `sms_logs_event_id_unique` ON `sms_logs` (`event_id`);