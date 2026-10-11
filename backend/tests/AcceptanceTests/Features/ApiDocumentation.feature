Feature: API documentation

  Scenario Outline: The v1 OpenAPI document describes the users endpoints
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the response status is 200
    And the OpenAPI document lists the path "<path>"

    Examples:
      | path                      |
      | /v1/users                 |
      | /v1/users/active          |
      | /v1/users/{id}            |
      | /v1/users/{id}/activate   |
      | /v1/users/{id}/deactivate |
      | /v1/expense-reports       |
      | /v1/expense-reports/{id}  |

  Scenario Outline: The v1 OpenAPI document describes the error responses
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the OpenAPI document declares the response "<status>" for "<method>" on "<path>"

    Examples:
      | method | path                      | status |
      | get    | /v1/users                 | 400    |
      | get    | /v1/users/active          | 400    |
      | get    | /v1/users/{id}            | 404    |
      | post   | /v1/users                 | 400    |
      | put    | /v1/users/{id}            | 400    |
      | put    | /v1/users/{id}            | 404    |
      | put    | /v1/users/{id}            | 409    |
      | put    | /v1/users/{id}            | 412    |
      | post   | /v1/users/{id}/activate   | 404    |
      | post   | /v1/users/{id}/activate   | 409    |
      | post   | /v1/users/{id}/deactivate | 404    |
      | post   | /v1/users/{id}/deactivate | 409    |
      | delete | /v1/users/{id}            | 404    |
      | delete | /v1/users/{id}            | 409    |
      | get    | /v1/expense-reports       | 400    |
      | get    | /v1/expense-reports/{id}  | 404    |
      | post   | /v1/expense-reports       | 400    |
      | post   | /v1/expense-reports       | 404    |
      | post   | /v1/expense-reports       | 422    |
      | delete | /v1/expense-reports/{id}  | 404    |

  Scenario: The v1 OpenAPI document describes the active filter of the users list
    Given the API runs in the Development environment
    When I request "/openapi/v1.json"
    Then the OpenAPI document declares the query parameter "isActive" for "get" on "/v1/users"

  Scenario: The Scalar reference is served in Development
    Given the API runs in the Development environment
    When I request "/scalar/"
    Then the response status is 200

  Scenario: API documentation is not exposed outside Development
    When I request "/openapi/v1.json"
    Then the response is a problem with status 404
