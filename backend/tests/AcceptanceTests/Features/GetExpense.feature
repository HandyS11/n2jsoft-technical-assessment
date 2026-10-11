Feature: Get an expense

  Background:
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
      | Juste Leblanc | 2025-11 |

  Scenario: Getting an expense returns its details and its version as ETag
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description | Amount | Store | Street           | Postal code | City  |
      | 2025-10-15 | Taxi        | 23.50  | G7    | 12 rue de Rivoli | 75001       | Paris |
    When I get the expense "Taxi"
    Then the response status is 200
    And the expense returned is
      | Date       | Description | Amount | Store | Street           | Postal code | City  |
      | 2025-10-15 | Taxi        | 23.50  | G7    | 12 rue de Rivoli | 75001       | Paris |
    And the response ETag is the version of the expense returned

  Scenario: An expense is only found through its own report
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-15 | Taxi        |
    When I get the expense "Taxi" through the report "Juste Leblanc - Novembre 2025"
    Then the response is a problem with status 404

  Scenario: A deleted expense cannot be read
    Given the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description | Status  |
      | 2025-10-15 | Taxi        | Deleted |
    When I get the expense "Taxi"
    Then the response is a problem with status 404

  Scenario: Getting an unknown expense answers not found
    When I get an unknown expense of "Juste Leblanc - Octobre 2025"
    Then the response is a problem with status 404
