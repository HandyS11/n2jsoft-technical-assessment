Feature: Create an expense report

  Scenario: Creating an expense report answers with the titled report and where to find it
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I create an expense report for "Juste Leblanc" for "2025-10"
    Then the response status is 201
    And the response locates the created expense report
    And the expense report returned is
      | Title                        | User          | Month   |
      | Juste Leblanc - Octobre 2025 | Juste Leblanc | 2025-10 |

  Scenario: The created expense report appears in the list
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I create an expense report for "Juste Leblanc" for "2025-10"
    And I list the expense reports
    Then the expense reports listed are
      | Title                        | Month   |
      | Juste Leblanc - Octobre 2025 | 2025-10 |

  Scenario Outline: The first and the last month allowed are accepted
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When I create an expense report for "Juste Leblanc" for "<month>"
    Then the response status is 201

    Examples:
      | month   |
      | 2000-01 |
      | 2100-12 |

  Scenario: A user can have reports for several months, and several users for the same month
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
      | Marc       | Assin     |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I create an expense report for "Juste Leblanc" for "2025-11"
    Then the response status is 201
    When I create an expense report for "Marc Assin" for "2025-10"
    Then the response status is 201

  Scenario: A second report for the same user and month is refused
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    And the following expense reports exist
      | User          | Month   |
      | Juste Leblanc | 2025-10 |
    When I create an expense report for "Juste Leblanc" for "2025-10"
    Then the response is a business rule violation with the code "expense_report.already_exists"

  Scenario: Two requests creating the same report at the same time create it once
    Given the following users exist
      | First name | Last name |
      | Juste      | Leblanc   |
    When two requests create an expense report for "Juste Leblanc" for "2025-10" at the same time
    Then the second request is refused as a duplicate
    When I list the expense reports
    Then the listing reports 1 expense reports in total

  Scenario: An inactive user cannot be assigned a report
    Given the following users exist
      | First name | Last name | Status   |
      | Juste      | Leblanc   | Inactive |
    When I create an expense report for "Juste Leblanc" for "2025-10"
    Then the response is a business rule violation with the code "expense_report.inactive_user"

  Scenario: A deleted user cannot be assigned a report
    Given the following users exist
      | First name | Last name | Status  |
      | Juste      | Leblanc   | Deleted |
    When I create an expense report for "Juste Leblanc" for "2025-10"
    Then the response is a problem with status 404

  Scenario: An unknown user cannot be assigned a report
    When I create an expense report for an unknown user
    Then the response is a problem with status 404

  Scenario Outline: Creating an expense report rejects an invalid <field>
    When I create an expense report whose <field> is <value>
    Then the response is a problem with status 400

    Examples:
      | field  | value        |
      | userId | null         |
      | userId | "not-a-guid" |
      | year   | null         |
      | year   | 1999         |
      | year   | 2101         |
      | month  | null         |
      | month  | 0            |
      | month  | 13           |
