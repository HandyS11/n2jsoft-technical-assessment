Feature: Delete an expense

  Background:
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    And the following expenses exist in "Juste Leblanc - Octobre 2025"
      | Date       | Description |
      | 2025-10-02 | Hôtel       |
      | 2025-10-15 | Taxi        |

  Scenario: A deleted expense disappears but stays stored
    When I delete the expense "Taxi"
    Then the response status is 204
    And the expense "Taxi" is still stored, marked as deleted
    When I list the expenses of "Juste Leblanc - Octobre 2025"
    Then the expenses listed are
      | Date       | Description | Amount |
      | 2025-10-02 | Hôtel       | 10.00  |

  Scenario: Deleting an expense twice answers not found
    When I delete the expense "Taxi"
    And I delete the expense "Taxi"
    Then the response is a problem with status 404

  Scenario: A deleted user's report keeps its expenses
    When I delete the user "Juste Leblanc"
    And I delete the expense "Taxi"
    Then the response is a business rule violation with the code "expense_report.deleted_user"

  Scenario: Deleting an unknown expense answers not found
    When I delete an unknown expense of "Juste Leblanc - Octobre 2025"
    Then the response is a problem with status 404
