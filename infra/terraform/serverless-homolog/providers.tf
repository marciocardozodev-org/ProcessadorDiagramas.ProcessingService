provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "ProcessadorDiagramas.ProcessingService"
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}
