variable "aws_region" {
  type        = string
  description = "AWS region for homolog stack"
}

variable "environment" {
  type        = string
  description = "Environment name"
  default     = "homolog"
}

variable "name_prefix" {
  type        = string
  description = "Resource name prefix"
  default     = "proc-diagramas"
}

variable "subnet_ids" {
  type        = list(string)
  description = "Private subnet ids for Lambda execution"
}

variable "security_group_ids" {
  type        = list(string)
  description = "Security groups for Lambda execution"
}

variable "lambda_memory_size" {
  type        = number
  description = "Lambda function memory in MB"
  default     = 512
}

variable "lambda_timeout" {
  type        = number
  description = "Lambda function timeout in seconds"
  default     = 60
}

variable "lambda_reserved_concurrency" {
  type        = number
  nullable    = true
  description = "Reserved concurrent executions (-1 = unreserved)"
  default     = null
}

variable "image_uri" {
  type        = string
  description = "Full container image URI in ECR"
}

variable "db_connection_ssm_parameter_arn" {
  type        = string
  description = "ARN of SSM parameter containing ConnectionStrings__DefaultConnection"
}

variable "ai_api_key_secret_arn" {
  type        = string
  description = "ARN of Secrets Manager secret containing AiProvider__ApiKey"
}

variable "ai_provider_base_url" {
  type    = string
  default = "https://api.groq.com/openai/v1"
}

variable "ai_provider_model" {
  type    = string
  default = "llama-3.1-8b-instant"
}

variable "ai_provider_temperature" {
  type    = string
  default = "0.2"
}

variable "ai_provider_max_tokens" {
  type    = string
  default = "500"
}

variable "ai_provider_max_input_characters" {
  type    = string
  default = "12000"
}

variable "ai_provider_timeout_seconds" {
  type    = string
  default = "30"
}

variable "alarm_actions" {
  type        = list(string)
  description = "SNS topics for CloudWatch alarms"
  default     = []
}
