from typing import Literal
from pydantic import BaseModel, Field, ConfigDict

CATEGORIES = Literal['Passport', 'Visa', 'Employment Pass', 'Dependant Pass', 'Student Pass',
    'Work Permit', 'Driving Licence', 'Employment Contract', 'Offer Letter', 'Insurance Document',
    'Tax Document', 'Bank Statement', 'Government Letter', 'Government Form', 'Rental Agreement',
    'Tenancy Agreement', 'Utility Bill', 'Medical Document', 'Educational Certificate',
    'Immigration Document', 'Identity Document', 'Financial Document', 'Certificate', 'Other', 'Unknown']

class StrictModel(BaseModel):
    model_config = ConfigDict(extra='forbid')

class Evidence(StrictModel):
    id: str
    page: int = Field(ge=1)
    section: str | None = None
    sourceText: str = Field(min_length=1, max_length=4000)
    boundingBox: list[float] | None = None

class Statement(StrictModel):
    id: str
    kind: Literal['fact', 'interpretation', 'finding', 'action', 'warning', 'clause', 'entity', 'date', 'money', 'obligation', 'condition', 'relationship', 'section']
    label: str = Field(min_length=1, max_length=200)
    text: str = Field(min_length=1, max_length=2000)
    originalValue: str = Field(min_length=1, max_length=4000)
    normalizedValue: str | None = None
    confidence: float = Field(ge=0, le=1)
    evidenceIds: list[str] = Field(min_length=1)
    supportStatus: Literal["SUPPORTED", "PARTIALLY_SUPPORTED", "UNSUPPORTED", "UNCERTAIN"] = "UNCERTAIN"
    supportReason: str | None = None

class SemanticDocument(StrictModel):
    documentCategory: CATEGORIES
    classificationConfidence: float = Field(ge=0, le=1)
    classificationEvidenceIds: list[str] = Field(min_length=1)
    statements: list[Statement] = Field(min_length=1, max_length=300)
    evidence: list[Evidence] = Field(min_length=1, max_length=300)

class Block(StrictModel):
    text: str
    boundingBox: list[float]
    kind: str = 'paragraph'

class Page(StrictModel):
    page: int
    text: str
    width: float
    height: float
    extraction: str
    rotationDegrees: int = 0
    coordinateSpace: str = "pdf_points"
    extractionConfidence: float = Field(default=1, ge=0, le=1)
    blocks: list[Block]
